using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;

namespace ShopHoa.Helpers;

/// <summary>
/// Khắc phục triệt để các lỗi nhập tiếng Việt (Microsoft Vietnamese IME/TSF, UniKey, EVKey, OpenKey)
/// trên Avalonia Win32:
/// 1) Ngăn Avalonia cho WM_IME_COMPOSITION rơi xuống DefWindowProc (vốn sinh WM_IME_CHAR/WM_CHAR trùng
///    gây nhân đôi ký tự - Issue #107), đồng thời bỏ chặn _ignoreWmChar / IsComposing sai thời điểm
///    khiến phím Space (hoặc ký tự sau VK_PROCESSKEY / VK_PACKET) bị nuốt (Issue #90).
/// 2) Bảo toàn từ đang gõ khi IME gửi WM_IME_COMPOSITION(lParam=0) / GCS_COMPSTR rỗng ngay trước
///    WM_IME_ENDCOMPOSITION, hoặc khi Imm32InputMethod.Reset() / _ignoreComposition làm mất trạng thái.
/// 3) Chốt phần preedit còn dang dở vào TextBox khi mất focus (bấm Lưu/chuyển ô) bằng đúng hằng số
///    Win32 IMM32 (NI_COMPOSITIONSTR = 0x0015, CPS_CANCEL = 0x0004), đồng thời tự gắn cho cả PopupRoot.
/// </summary>
public class WindowsImeTextInputFix
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmChar = 0x0102;
    private const uint WmImeStartComposition = 0x010D;
    private const uint WmImeEndComposition = 0x010E;
    private const uint WmImeComposition = 0x010F;
    private const uint WmImeChar = 0x0286;

    private const int GcsCompStr = 0x0008;
    private const int GcsResultStr = 0x0800;

    private const long VkBack = 0x08;
    private const long VkEscape = 0x1B;
    private const int ScanBackspace = 0x0E;
    private const int ScanEscape = 0x01;

    // Hằng số chuẩn Win32 imm.h: NI_COMPOSITIONSTR = 0x0015 (21), CPS_CANCEL = 0x0004.
    private const uint NiCompositionStr = 0x0015;
    private const uint CpsCancel = 0x0004;

    private const long DuplicateGuardWindowMs = 80;

    private static readonly ConditionalWeakTable<TopLevel, HookState> s_states = new();

    private static readonly ConcurrentDictionary<Type, FieldInfo?> s_ignoreWmCharFieldCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> s_compositionPropCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> s_isComposingPropCache = new();
    private static readonly ConcurrentDictionary<Type, FieldInfo?> s_ignoreCompositionFieldCache = new();
    private static readonly ConcurrentDictionary<Type, FieldInfo?> s_currentHimcFieldCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo?> s_handleCompositionMethodCache = new();

    private static readonly FieldInfo? s_textBoxPresenterField = typeof(TextBox).GetField(
        "_presenter",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? s_textBoxImClientField = typeof(TextBox).GetField(
        "_imClient",
        BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Hook phục vụ kiểm thử tự động trong ShopHoa.Tests khi không có IME OS thực sự gắn vào HWND.
    /// Mặc định null trong quá trình chạy phần mềm.
    /// </summary>
    internal static Func<IntPtr, uint, string?>? TestCompositionStringReader { get; set; }

    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<WindowsImeTextInputFix, TopLevel, bool>("Enabled");

    static WindowsImeTextInputFix()
    {
        EnabledProperty.Changed.AddClassHandler<TopLevel>(OnEnabledChanged);
        InputElement.GotFocusEvent.AddClassHandler<TextBox>(
            OnTextBoxGotFocus,
            RoutingStrategies.Direct | RoutingStrategies.Bubble,
            handledEventsToo: true);
        InputElement.LostFocusEvent.AddClassHandler<TextBox>(
            OnTextBoxLostFocus,
            RoutingStrategies.Direct | RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    public static void SetEnabled(TopLevel topLevel, bool value) => topLevel.SetValue(EnabledProperty, value);

    public static bool GetEnabled(TopLevel topLevel) => topLevel.GetValue(EnabledProperty);

    private static void OnEnabledChanged(TopLevel topLevel, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.GetNewValue<bool>())
            EnsureAttached(topLevel);
    }

    private static void OnTextBoxGotFocus(TextBox textBox, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(textBox) is not { } topLevel)
            return;

        if (!GetEnabled(topLevel))
            SetEnabled(topLevel, true);
        else
            EnsureAttached(topLevel);
    }

    internal static void EnsureAttached(TopLevel topLevel)
    {
        var state = s_states.GetValue(topLevel, static _ => new HookState());
        if (state.HookRegistered)
            return;

        if (topLevel.PlatformImpl is not null)
        {
            state.HookRegistered = true;
            Register(topLevel, state);
            return;
        }

        if (!state.WaitingOpened)
        {
            state.WaitingOpened = true;
            topLevel.Opened += OnTopLevelOpened;
        }
    }

    private static void OnTopLevelOpened(object? sender, EventArgs e)
    {
        if (sender is not TopLevel topLevel)
            return;

        topLevel.Opened -= OnTopLevelOpened;
        EnsureAttached(topLevel);
    }

    private static void Register(TopLevel topLevel, HookState state)
    {
        Win32Properties.AddWndProcHookCallback(
            topLevel,
            (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                ProcessWndProcMessage(topLevel, state, hWnd, msg, wParam, lParam, ref handled));
    }

    private static IntPtr ProcessWndProcMessage(
        TopLevel topLevel,
        HookState state,
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        switch (msg)
        {
            case WmKeyDown:
            case WmSysKeyDown:
                HandleKeyDown(topLevel, state, wParam, lParam);
                break;

            case WmImeStartComposition:
                HandleImeStartComposition(topLevel, state);
                break;

            case WmImeComposition:
                HandleImeComposition(topLevel, state, hWnd, wParam, lParam, ref handled);
                break;

            case WmImeEndComposition:
                HandleImeEndComposition(topLevel, state);
                break;

            case WmImeChar:
                if (HasFreshPendingDuplicates(state))
                    handled = true;
                break;

            case WmChar:
                if (HandleChar(topLevel, state, wParam))
                    handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private static void HandleKeyDown(TopLevel topLevel, HookState state, IntPtr wParam, IntPtr lParam)
    {
        state.PendingDuplicateResultChars.Clear();
        state.LastResultEndedWithSpace = false;

        long vk = wParam.ToInt64();
        int scanCode = unchecked((int)((lParam.ToInt64() >> 16) & 0xFF));
        bool isCancelKey = vk == VkBack || vk == VkEscape || scanCode == ScanBackspace || scanCode == ScanEscape;

        state.CanceledInCurrentComposition = state.InComposition && isCancelKey;

        if (!state.InComposition)
        {
            if (GetIsComposing(topLevel) && string.IsNullOrEmpty(GetComposition(topLevel)))
                SetIsComposing(topLevel, false);

            if (isCancelKey && topLevel.FocusManager?.GetFocusedElement() is TextBox focusedTb)
                ClearPreedit(focusedTb, topLevel);
        }
    }

    private static void HandleImeStartComposition(TopLevel topLevel, HookState state)
    {
        state.InComposition = true;
        state.CommittedInCurrentComposition = false;
        state.CanceledInCurrentComposition = false;
        state.LastNonEmptyCompStr = null;
        state.PendingDuplicateResultChars.Clear();
        state.LastResultEndedWithSpace = false;

        SetIgnoreComposition(topLevel, false);
        SetIsComposing(topLevel, true);
    }

    private static void HandleImeComposition(
        TopLevel topLevel,
        HookState state,
        IntPtr hWnd,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        SetIgnoreComposition(topLevel, false);
        SetIsComposing(topLevel, true);

        int flags = unchecked((int)lParam.ToInt64());
        string? resultStr = (flags & GcsResultStr) != 0 ? ReadImmCompositionString(hWnd, (uint)GcsResultStr) : null;
        string? compStr = (flags & GcsCompStr) != 0 ? ReadImmCompositionString(hWnd, (uint)GcsCompStr) : null;

        if (!string.IsNullOrEmpty(resultStr))
        {
            state.CommittedInCurrentComposition = true;
            state.LastNonEmptyCompStr = null;
            RecordPendingDuplicates(state, resultStr);
        }

        bool compEqualsResultOrEmpty =
            string.IsNullOrEmpty(compStr) || string.Equals(compStr, resultStr, StringComparison.Ordinal);

        IntPtr effectiveLParam = lParam;
        if (!string.IsNullOrEmpty(resultStr) && (flags & GcsCompStr) != 0 && compEqualsResultOrEmpty)
            effectiveLParam = new IntPtr(flags & ~GcsCompStr);

        bool hasActiveNewComp =
            !string.IsNullOrEmpty(compStr) && !string.Equals(compStr, resultStr, StringComparison.Ordinal);

        if (hasActiveNewComp)
        {
            state.LastNonEmptyCompStr = compStr;
            state.InComposition = true;
            state.CommittedInCurrentComposition = false;
            state.CanceledInCurrentComposition = false;
        }

        var focusedTb = topLevel.FocusManager?.GetFocusedElement() as TextBox;

        if (TestCompositionStringReader is not null)
        {
            ApplyCompositionInTestMode(topLevel, focusedTb, flags, resultStr, compStr, hasActiveNewComp);
        }
        else
        {
            bool invoked = InvokeAvaloniaHandleComposition(topLevel, hWnd, wParam, effectiveLParam);
            if (!invoked && focusedTb is not null)
            {
                if (!string.IsNullOrEmpty(resultStr))
                {
                    ClearPreedit(focusedTb, topLevel);
                    InsertTextAtCaret(focusedTb, resultStr);
                    SetIgnoreWmChar(topLevel, true);
                }
                else if (hasActiveNewComp)
                {
                    SetComposition(topLevel, compStr);
                    SetPreedit(focusedTb, topLevel, compStr);
                }
            }
        }

        if (!string.IsNullOrEmpty(resultStr) && compEqualsResultOrEmpty)
        {
            SetComposition(topLevel, null);
            if (focusedTb is not null)
                ClearPreedit(focusedTb, topLevel);
            SetIsComposing(topLevel, false);
            state.InComposition = false;
        }

        // Chặn rơi xuống DefWindowProc(hWnd, WM_IME_COMPOSITION, ...) để Windows không phát
        // sinh WM_IME_CHAR -> WM_CHAR trùng lặp với GCS_RESULTSTR.
        handled = true;
    }

    private static void HandleImeEndComposition(TopLevel topLevel, HookState state)
    {
        if (state.CommittedInCurrentComposition)
        {
            SetComposition(topLevel, null);
            if (topLevel.FocusManager?.GetFocusedElement() is TextBox tb)
                ClearPreedit(tb, topLevel);
        }
        else if (!state.CanceledInCurrentComposition && !string.IsNullOrEmpty(state.LastNonEmptyCompStr))
        {
            // TSF/CUAS có thể gửi lParam=0 hoặc GCS_COMPSTR="" để xóa preedit trước WM_IME_ENDCOMPOSITION
            // khi nhấn Space. Phục hồi LastNonEmptyCompStr để HandleCompositionEnd chèn đủ từ vào ô nhập.
            SetIgnoreComposition(topLevel, false);
            SetIsComposing(topLevel, true);
            SetComposition(topLevel, state.LastNonEmptyCompStr);
            RecordPendingDuplicates(state, state.LastNonEmptyCompStr);

            if (TestCompositionStringReader is not null
                && topLevel.FocusManager?.GetFocusedElement() is TextBox tb
                && !tb.IsReadOnly
                && tb.IsEnabled)
            {
                ClearPreedit(tb, topLevel);
                InsertTextAtCaret(tb, state.LastNonEmptyCompStr);
                SetComposition(topLevel, null);
                SetIsComposing(topLevel, false);
                SetIgnoreWmChar(topLevel, true);
            }
        }

        state.InComposition = false;
        state.CommittedInCurrentComposition = false;
        state.CanceledInCurrentComposition = false;
        state.LastNonEmptyCompStr = null;
    }

    private static bool HandleChar(TopLevel topLevel, HookState state, IntPtr wParam)
    {
        int codePoint = unchecked((int)wParam.ToInt64());
        if (codePoint < 32)
            return false;

        if (topLevel.FocusManager?.GetFocusedElement() is not TextBox focusedTb)
            return false;

        bool hasActivePreedit = state.InComposition && !string.IsNullOrEmpty(GetComposition(topLevel));
        if (hasActivePreedit)
            return false;

        char ch = (char)codePoint;

        if (HasFreshPendingDuplicates(state))
        {
            if (ch == ' ' && state.LastResultEndedWithSpace)
            {
                state.LastResultEndedWithSpace = false;
                SetIgnoreWmChar(topLevel, true);
                return true;
            }

            if (ch != ' '
                && state.PendingDuplicateResultChars.Count > 0
                && state.PendingDuplicateResultChars.Peek() == ch)
            {
                state.PendingDuplicateResultChars.Dequeue();
                SetIgnoreWmChar(topLevel, true);
                return true;
            }
        }

        state.PendingDuplicateResultChars.Clear();
        state.LastResultEndedWithSpace = false;

        ClearPreedit(focusedTb, topLevel);
        SetIsComposing(topLevel, false);
        SetIgnoreWmChar(topLevel, false);
        return false;
    }

    private static void OnTextBoxLostFocus(TextBox textBox, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(textBox);
        if (topLevel is null || !GetEnabled(topLevel))
            return;

        s_states.TryGetValue(topLevel, out var state);

        string? uncommitted = null;
        if (state is null || (!state.CommittedInCurrentComposition && !state.CanceledInCurrentComposition))
        {
            uncommitted = FirstNonEmpty(
                GetComposition(topLevel),
                state?.LastNonEmptyCompStr,
                GetPreeditText(textBox));
        }

        if (state is not null)
        {
            state.InComposition = false;
            state.CommittedInCurrentComposition = false;
            state.CanceledInCurrentComposition = true;
            state.LastNonEmptyCompStr = null;
            state.PendingDuplicateResultChars.Clear();
            state.LastResultEndedWithSpace = false;
        }

        ClearPreedit(textBox, topLevel);
        ClearCompositionState(topLevel);
        CancelNativeComposition(topLevel);

        if (!string.IsNullOrEmpty(uncommitted) && textBox.IsEnabled && !textBox.IsReadOnly)
            InsertTextAtCaret(textBox, uncommitted);
    }

    private static void ApplyCompositionInTestMode(
        TopLevel topLevel,
        TextBox? focusedTb,
        int flags,
        string? resultStr,
        string? compStr,
        bool hasActiveNewComp)
    {
        if (!string.IsNullOrEmpty(resultStr))
        {
            SetComposition(topLevel, null);
            if (focusedTb is not null)
            {
                ClearPreedit(focusedTb, topLevel);
                if (focusedTb.IsEnabled && !focusedTb.IsReadOnly)
                    InsertTextAtCaret(focusedTb, resultStr);
            }

            SetIgnoreWmChar(topLevel, true);
        }

        if (hasActiveNewComp)
        {
            SetComposition(topLevel, compStr);
            if (focusedTb is not null)
                SetPreedit(focusedTb, topLevel, compStr);
        }
        else if (string.IsNullOrEmpty(resultStr)
            && (flags == 0 || ((flags & GcsCompStr) != 0 && string.IsNullOrEmpty(compStr))))
        {
            SetComposition(topLevel, string.Empty);
            if (focusedTb is not null)
                SetPreedit(focusedTb, topLevel, string.Empty);
        }
    }

    private static bool InvokeAvaloniaHandleComposition(
        TopLevel topLevel,
        IntPtr hWnd,
        IntPtr wParam,
        IntPtr effectiveLParam)
    {
        var inputMethod = GetInputMethod(topLevel);
        if (inputMethod is null)
            return false;

        var type = inputMethod.GetType();
        var method = s_handleCompositionMethodCache.GetOrAdd(
            type,
            static t => t.GetMethod(
                "HandleComposition",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        if (method is null)
            return false;

        var himcField = s_currentHimcFieldCache.GetOrAdd(
            type,
            static t => t.GetField("_currentHimc", BindingFlags.Instance | BindingFlags.NonPublic));

        IntPtr previousHimc = himcField?.GetValue(inputMethod) is IntPtr p ? p : IntPtr.Zero;
        IntPtr acquiredHimc = IntPtr.Zero;

        try
        {
            if (previousHimc == IntPtr.Zero && OperatingSystem.IsWindows() && hWnd != IntPtr.Zero)
            {
                acquiredHimc = ImmGetContext(hWnd);
                if (acquiredHimc != IntPtr.Zero && himcField is not null)
                    himcField.SetValue(inputMethod, acquiredHimc);
            }

            if (previousHimc == IntPtr.Zero && acquiredHimc == IntPtr.Zero)
                return false;

            uint timestamp = unchecked((uint)(OperatingSystem.IsWindows() ? GetMessageTime() : Environment.TickCount));
            method.Invoke(inputMethod, new object?[] { wParam, effectiveLParam, timestamp });
            return true;
        }
        finally
        {
            if (acquiredHimc != IntPtr.Zero)
            {
                if (himcField is not null)
                    himcField.SetValue(inputMethod, IntPtr.Zero);
                ImmReleaseContext(hWnd, acquiredHimc);
            }
        }
    }

    private static string? ReadImmCompositionString(IntPtr hWnd, uint flag)
    {
        if (TestCompositionStringReader is { } testReader)
            return testReader(hWnd, flag);

        if (!OperatingSystem.IsWindows() || hWnd == IntPtr.Zero)
            return null;

        var himc = ImmGetContext(hWnd);
        if (himc == IntPtr.Zero)
            return null;

        try
        {
            int byteLength = ImmGetCompositionStringW(himc, flag, null, 0);
            if (byteLength <= 0)
                return byteLength == 0 ? string.Empty : null;

            var buffer = new byte[byteLength];
            int copied = ImmGetCompositionStringW(himc, flag, buffer, byteLength);
            if (copied <= 0)
                return null;

            return Encoding.Unicode.GetString(buffer, 0, copied);
        }
        finally
        {
            ImmReleaseContext(hWnd, himc);
        }
    }

    private static void RecordPendingDuplicates(HookState state, string text)
    {
        state.PendingDuplicateResultChars.Clear();
        foreach (var ch in text)
            state.PendingDuplicateResultChars.Enqueue(ch);

        state.LastResultEndedWithSpace = text.EndsWith(' ');
        state.LastResultTickMs = Environment.TickCount64;
    }

    private static bool HasFreshPendingDuplicates(HookState state)
    {
        if (state.PendingDuplicateResultChars.Count == 0 && !state.LastResultEndedWithSpace)
            return false;

        if (Environment.TickCount64 - state.LastResultTickMs <= DuplicateGuardWindowMs)
            return true;

        state.PendingDuplicateResultChars.Clear();
        state.LastResultEndedWithSpace = false;
        return false;
    }

    private static void InsertTextAtCaret(TextBox textBox, string insertText)
    {
        var current = textBox.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(textBox.SelectionStart, textBox.SelectionEnd), 0, current.Length);
        var end = Math.Clamp(Math.Max(textBox.SelectionStart, textBox.SelectionEnd), start, current.Length);

        if (textBox.MaxLength > 0)
        {
            int remaining = current.Length - (end - start);
            int allowed = Math.Max(0, textBox.MaxLength - remaining);
            if (insertText.Length > allowed)
                insertText = insertText.Substring(0, allowed);
        }

        if (insertText.Length == 0 && start == end)
            return;

        textBox.Text = current.Remove(start, end - start).Insert(start, insertText);
        var caret = start + insertText.Length;
        textBox.CaretIndex = caret;
        textBox.SelectionStart = caret;
        textBox.SelectionEnd = caret;
    }

    private static ITextInputMethodImpl? GetInputMethod(TopLevel topLevel)
        => topLevel is ITextInputMethodRoot root ? root.InputMethod : null;

    private static string? GetComposition(TopLevel topLevel)
    {
        var inputMethod = GetInputMethod(topLevel);
        if (inputMethod is null)
            return null;

        var property = s_compositionPropCache.GetOrAdd(
            inputMethod.GetType(),
            static t => t.GetProperty("Composition", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        return property?.GetValue(inputMethod) as string;
    }

    private static void SetComposition(TopLevel topLevel, string? value)
    {
        var inputMethod = GetInputMethod(topLevel);
        if (inputMethod is null)
            return;

        var property = s_compositionPropCache.GetOrAdd(
            inputMethod.GetType(),
            static t => t.GetProperty("Composition", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        property?.GetSetMethod(nonPublic: true)?.Invoke(inputMethod, new object?[] { value });
    }

    private static bool GetIsComposing(TopLevel topLevel)
    {
        var inputMethod = GetInputMethod(topLevel);
        if (inputMethod is null)
            return false;

        var property = s_isComposingPropCache.GetOrAdd(
            inputMethod.GetType(),
            static t => t.GetProperty("IsComposing", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        return property?.GetValue(inputMethod) is true;
    }

    private static void SetIsComposing(TopLevel topLevel, bool value)
    {
        var inputMethod = GetInputMethod(topLevel);
        if (inputMethod is null)
            return;

        var property = s_isComposingPropCache.GetOrAdd(
            inputMethod.GetType(),
            static t => t.GetProperty("IsComposing", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        property?.GetSetMethod(nonPublic: true)?.Invoke(inputMethod, new object?[] { value });
    }

    private static void SetIgnoreComposition(TopLevel topLevel, bool value)
    {
        var inputMethod = GetInputMethod(topLevel);
        if (inputMethod is null)
            return;

        var field = s_ignoreCompositionFieldCache.GetOrAdd(
            inputMethod.GetType(),
            static t => t.GetField("_ignoreComposition", BindingFlags.Instance | BindingFlags.NonPublic));

        field?.SetValue(inputMethod, value);
    }

    private static void ClearCompositionState(TopLevel topLevel)
    {
        SetIgnoreComposition(topLevel, false);
        SetIsComposing(topLevel, false);
        SetComposition(topLevel, null);
    }

    private static string? GetPreeditText(TextBox textBox)
    {
        if (s_textBoxPresenterField?.GetValue(textBox) is TextPresenter presenter)
            return presenter.PreeditText;

        return null;
    }

    private static void SetPreedit(TextBox textBox, TopLevel? topLevel, string? value)
    {
        if (s_textBoxImClientField?.GetValue(textBox) is TextInputMethodClient imClient)
            imClient.SetPreeditText(value);

        if (s_textBoxPresenterField?.GetValue(textBox) is TextPresenter presenter)
            presenter.PreeditText = value;

        if (topLevel is not null && string.IsNullOrEmpty(value))
            SetIsComposing(topLevel, false);
    }

    private static void ClearPreedit(TextBox textBox, TopLevel? topLevel)
    {
        if (s_textBoxImClientField?.GetValue(textBox) is TextInputMethodClient imClient)
            imClient.SetPreeditText(null);

        if (s_textBoxPresenterField?.GetValue(textBox) is TextPresenter presenter && presenter.PreeditText is not null)
            presenter.PreeditText = null;
    }

    private static void SetIgnoreWmChar(TopLevel topLevel, bool value)
    {
        if (topLevel.PlatformImpl is not { } platformImpl)
            return;

        var field = s_ignoreWmCharFieldCache.GetOrAdd(
            platformImpl.GetType(),
            static type => type.GetField(
                "_ignoreWmChar",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        field?.SetValue(platformImpl, value);
    }

    private static void CancelNativeComposition(TopLevel topLevel)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var hwnd = topLevel.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
            return;

        var himc = ImmGetContext(hwnd);
        if (himc == IntPtr.Zero)
            return;

        try
        {
            ImmNotifyIME(himc, NiCompositionStr, CpsCancel, 0);
        }
        finally
        {
            ImmReleaseContext(hwnd, himc);
        }
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate))
                return candidate;
        }

        return null;
    }

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmNotifyIME(IntPtr hIMC, uint action, uint index, uint value);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    private static extern int ImmGetCompositionStringW(
        IntPtr hIMC,
        uint dwIndex,
        [Out] byte[]? lpBuf,
        int dwBufLen);

    [DllImport("user32.dll")]
    private static extern int GetMessageTime();

    private sealed class HookState
    {
        public bool HookRegistered;
        public bool WaitingOpened;
        public bool InComposition;
        public bool CommittedInCurrentComposition;
        public bool CanceledInCurrentComposition;
        public string? LastNonEmptyCompStr;
        public readonly Queue<char> PendingDuplicateResultChars = new();
        public bool LastResultEndedWithSpace;
        public long LastResultTickMs;
    }
}
