using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ShopHoa.Helpers;

/// <summary>
/// Workaround cho lỗi Avalonia trên Windows (issue #12446): khi đổi ô nhập,
/// Imm32InputMethod.Reset() bật cờ _ignoreWmChar nên ký tự WM_CHAR kế tiếp bị bỏ,
/// khiến chữ vừa gõ (đặc biệt với UniKey/EVKey/OpenKey) bị mất.
/// Ta xóa cờ này ngay trước khi Avalonia xử lý WM_CHAR khi con trỏ đang ở ô nhập,
/// trừ khi ký tự đó là kết quả vừa được IME chèn qua composition (tránh nhân đôi).
/// </summary>
public class WindowsImeTextInputFix
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmChar = 0x0102;
    private const uint WmImeEndComposition = 0x010E;
    private const uint WmImeComposition = 0x010F;
    private const long GcsResultStr = 0x0800;
    private const string IgnoreWmCharFieldName = "_ignoreWmChar";

    private static readonly ConditionalWeakTable<TopLevel, object> s_attached = new();
    private static readonly ConcurrentDictionary<Type, FieldInfo?> s_fieldCache = new();

    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<WindowsImeTextInputFix, TopLevel, bool>("Enabled");

    static WindowsImeTextInputFix()
    {
        EnabledProperty.Changed.AddClassHandler<TopLevel>(OnEnabledChanged);
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
