using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;

namespace ShopHoa.Helpers;

/// <summary>
/// Workaround cho lỗi Avalonia trên Windows với bàn phím tiếng Việt:
/// 1) Khi đổi ô nhập, Imm32InputMethod.Reset() bật cờ _ignoreWmChar nên ký tự
///    WM_CHAR kế tiếp bị bỏ (issue #12446). Ta xóa cờ này ngay trước khi
///    Avalonia xử lý WM_CHAR khi con trỏ đang ở ô nhập, trừ khi ký tự đó là kết
///    quả vừa được IME chèn qua composition (tránh nhân đôi).
/// 2) Khi đổi ô nhập trong cùng cửa sổ, Avalonia gọi Reset() để hủy composition
///    đang gõ nên từ vừa gõ bị mất (PR #15907 chỉ xử lý khi cửa sổ mất focus).
///    Ta chèn nốt phần composition còn dang dở vào ô vừa mất focus trước khi
///    Avalonia hủy nó.
/// </summary>
public class WindowsImeTextInputFix
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmChar = 0x0102;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmImeEndComposition = 0x010E;
    private const uint WmImeComposition = 0x010F;
    private const long GcsResultStr = 0x0800;
    private const string IgnoreWmCharFieldName = "_ignoreWmChar";
    private const string CompositionPropertyName = "Composition";
    private const string IsComposingPropertyName = "IsComposing";
    private const uint NiCompositionStr = 0x0009;
    private const uint CpsCancel = 0x0002;

    private static readonly ConditionalWeakTable<TopLevel, object> s_attached = new();
    private static readonly ConcurrentDictionary<Type, FieldInfo?> s_fieldCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> s_compositionCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> s_isComposingCache = new();

    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<WindowsImeTextInputFix, TopLevel, bool>("Enabled");

    static WindowsImeTextInputFix()
    {
        EnabledProperty.Changed.AddClassHandler<TopLevel>(OnEnabledChanged);
        TextBox.LostFocusEvent.AddClassHandler<TextBox>(
            OnTextBoxLostFocus,
            RoutingStrategies.Direct | RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    public static void SetEnabled(TopLevel topLevel, bool value) => topLevel.SetValue(EnabledProperty, value);

    public static bool GetEnabled(TopLevel topLevel) => topLevel.GetValue(EnabledProperty);

    private static void OnEnabledChanged(TopLevel topLevel, AvaloniaPropertyChangedEventArgs args)
    {
        if (!args.GetNewValue<bool>() || !s_attached.TryAdd(topLevel, topLevel))
            return;

        if (topLevel.PlatformImpl is not null)
            Register(topLevel);
        else
            topLevel.Opened += OnOpened;
    }

    private static void OnOpened(object? sender, EventArgs e)
    {
        if (sender is not TopLevel topLevel)
            return;

        topLevel.Opened -= OnOpened;
        Register(topLevel);
    }

    private static void Register(TopLevel topLevel)
    {
        var state = new HookState();

        Win32Properties.AddWndProcHookCallback(
            topLevel,
            (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                switch (msg)
                {
                    case WmKeyDown:
                    case WmSysKeyDown:
                        state.CompositionResultPending = false;
                        break;

                    case WmImeComposition:
                        if ((lParam.ToInt64() & GcsResultStr) != 0)
                            state.CompositionResultPending = true;
                        break;

                    case WmImeEndComposition:
                        state.CompositionResultPending = true;
                        break;

                    case WmChar:
                        if (wParam.ToInt64() >= 32
                            && topLevel.FocusManager?.GetFocusedElement() is TextBox)
                        {
                            if (!state.CompositionResultPending)
                                ClearIgnoreWmChar(topLevel);

                            state.CompositionResultPending = false;
                        }
                        break;
                }

                return IntPtr.Zero;
            });
    }

    private static void OnTextBoxLostFocus(TextBox textBox, RoutedEventArgs e)
    {
        if (textBox.IsReadOnly)
            return;

        var topLevel = TopLevel.GetTopLevel(textBox);
        if (topLevel is null || !GetEnabled(topLevel))
            return;

        var composition = GetComposition(topLevel);
        if (string.IsNullOrEmpty(composition))
            return;

        var text = textBox.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(textBox.SelectionStart, textBox.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(textBox.SelectionStart, textBox.SelectionEnd), start, text.Length);

        textBox.Text = text.Remove(start, end - start).Insert(start, composition);
        textBox.CaretIndex = start + composition.Length;
        textBox.SelectionStart = textBox.SelectionEnd = start + composition.Length;

        // Đã chép composition vào ô cũ; xóa trạng thái composition để Avalonia không
        // chèn lại nó vào ô mới khi Reset() hoàn tất composition bên dưới.
        ClearComposition(topLevel);
        CancelNativeComposition(topLevel);
    }

    private static string? GetComposition(TopLevel topLevel)
    {
        var inputMethod = GetInputMethod(topLevel);
        if (inputMethod is null)
            return null;

        var property = s_compositionCache.GetOrAdd(
            inputMethod.GetType(),
            static type => type.GetProperty(
                CompositionPropertyName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        return property?.GetValue(inputMethod) as string;
    }

    private static ITextInputMethodImpl? GetInputMethod(TopLevel topLevel)
        => topLevel is ITextInputMethodRoot root ? root.InputMethod : null;

    private static void ClearComposition(TopLevel topLevel)
    {
        var inputMethod = GetInputMethod(topLevel);
        if (inputMethod is null)
            return;

        var type = inputMethod.GetType();

        var isComposing = s_isComposingCache.GetOrAdd(
            type,
            static t => t.GetProperty(
                IsComposingPropertyName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        isComposing?.GetSetMethod(nonPublic: true)?.Invoke(inputMethod, new object?[] { false });

        var composition = s_compositionCache.GetOrAdd(
            type,
            static t => t.GetProperty(
                CompositionPropertyName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        composition?.GetSetMethod(nonPublic: true)?.Invoke(inputMethod, new object?[] { null });
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

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmNotifyIME(IntPtr hIMC, uint action, uint index, uint value);

    private static void ClearIgnoreWmChar(TopLevel topLevel)
    {
        if (topLevel.PlatformImpl is not { } platformImpl)
            return;

        var field = s_fieldCache.GetOrAdd(
            platformImpl.GetType(),
            static type => type.GetField(
                IgnoreWmCharFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        field?.SetValue(platformImpl, false);
    }

    private sealed class HookState
    {
        public bool CompositionResultPending;
    }
}
