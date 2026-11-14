using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using ShopHoa.Models;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

/// <summary>
/// Toàn bộ giao diện màn "Tổng quan". DataContext là <c>DashboardViewModel</c>
/// nên mọi binding bên trong không cần tiền tố Dashboard.
/// </summary>
public partial class TongQuanDashboardView : UserControl
{
    public TongQuanDashboardView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => CapNhatBoCucResponsive(e.NewSize.Width);
        AttachedToVisualTree += (_, _) =>
        {
            if (Bounds.Width > 0)
                CapNhatBoCucResponsive(Bounds.Width);
        };
    }

    internal void CapNhatBoCucResponsive(double width)
    {
        if (width <= 0 || double.IsNaN(width) || double.IsInfinity(width))
            return;

        CapNhatBannerVaThongKeBanHang(width);
        CapNhatKhuVucHoaDonLoaiTruVaKho(width);
        CapNhatBieuDoVaTop10(width);
        CapNhatNhatKyHoatDong(width);
    }

    private void CapNhatBannerVaThongKeBanHang(double width)
    {
        if (EmptyPeriodBannerGrid is not null && EmptyPeriodBannerActions is not null)
        {
            if (width >= 1020)
            {
                EmptyPeriodBannerGrid.ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
                EmptyPeriodBannerGrid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(EmptyPeriodBannerActions, 0, 2);
                EmptyPeriodBannerActions.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                EmptyPeriodBannerGrid.ColumnDefinitions = new ColumnDefinitions("Auto,*");
                EmptyPeriodBannerGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(EmptyPeriodBannerActions, 1, 0, colSpan: 2);
                EmptyPeriodBannerActions.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        if (MetricsGrid is null)
            return;

        Control?[] cards = [MetricCard0, MetricCard1, MetricCard2, MetricCard3, MetricCard4];
        if (cards.Any(c => c is null))
            return;

        if (width >= 1080)
        {
            MetricsGrid.ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*");
            MetricsGrid.RowDefinitions = new RowDefinitions("Auto");
            for (var i = 0; i < cards.Length; i++)
                SetGridCell(cards[i]!, 0, i);
        }
        else if (width >= 680)
        {
            MetricsGrid.ColumnDefinitions = new ColumnDefinitions("*,*,*");
            MetricsGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            SetGridCell(cards[0]!, 0, 0);
            SetGridCell(cards[1]!, 0, 1);
            SetGridCell(cards[2]!, 0, 2);
            SetGridCell(cards[3]!, 1, 0);
            SetGridCell(cards[4]!, 1, 1);
        }
        else
        {
            MetricsGrid.ColumnDefinitions = new ColumnDefinitions("*,*");
            MetricsGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
            SetGridCell(cards[0]!, 0, 0);
            SetGridCell(cards[1]!, 0, 1);
            SetGridCell(cards[2]!, 1, 0);
            SetGridCell(cards[3]!, 1, 1);
            SetGridCell(cards[4]!, 2, 0, colSpan: 2);
        }
    }

    private void CapNhatKhuVucHoaDonLoaiTruVaKho(double width)
    {
        if (ExcludedHeaderGrid is not null && ExcludedHeaderActions is not null)
        {
            if (width >= 760)
            {
                ExcludedHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
                ExcludedHeaderGrid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(ExcludedHeaderActions, 0, 1);
                ExcludedHeaderActions.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                ExcludedHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*");
                ExcludedHeaderGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(ExcludedHeaderActions, 1, 0);
                ExcludedHeaderActions.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        if (ExcludedMetricsGrid is not null &&
            ExcludedCard0 is not null && ExcludedCard1 is not null &&
            ExcludedCard2 is not null && ExcludedCard3 is not null)
        {
            if (width >= 850)
            {
                ExcludedMetricsGrid.ColumnDefinitions = new ColumnDefinitions("*,*,*,*");
                ExcludedMetricsGrid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(ExcludedCard0, 0, 0);
                SetGridCell(ExcludedCard1, 0, 1);
                SetGridCell(ExcludedCard2, 0, 2);
                SetGridCell(ExcludedCard3, 0, 3);
            }
            else
            {
                ExcludedMetricsGrid.ColumnDefinitions = new ColumnDefinitions("*,*");
                ExcludedMetricsGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(ExcludedCard0, 0, 0);
                SetGridCell(ExcludedCard1, 0, 1);
                SetGridCell(ExcludedCard2, 1, 0);
                SetGridCell(ExcludedCard3, 1, 1);
            }
        }

        if (WarehouseGrid is null || WarehouseTitlePanel is null || WarehouseInventoryValueCard is null)
            return;

        Control?[] khoCards = [WarehouseCard0, WarehouseCard1, WarehouseCard2, WarehouseCard3, WarehouseCard4];
        if (khoCards.Any(c => c is null))
            return;

        if (width >= 980)
        {
            WarehouseGrid.ColumnDefinitions = new ColumnDefinitions("Auto,*,*,*,*,*");
            WarehouseGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            SetGridCell(WarehouseTitlePanel, 0, 0);
            WarehouseTitlePanel.Margin = new Thickness(0, 0, 6, 0);
            for (var i = 0; i < khoCards.Length; i++)
                SetGridCell(khoCards[i]!, 0, i + 1);
            SetGridCell(WarehouseInventoryValueCard, 1, 1, colSpan: 5);
        }
        else if (width >= 640)
        {
            WarehouseGrid.ColumnDefinitions = new ColumnDefinitions("*,*,*");
            WarehouseGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");
            SetGridCell(WarehouseTitlePanel, 0, 0, colSpan: 3);
            WarehouseTitlePanel.Margin = new Thickness(0);
            SetGridCell(khoCards[0]!, 1, 0);
            SetGridCell(khoCards[1]!, 1, 1);
            SetGridCell(khoCards[2]!, 1, 2);
            SetGridCell(khoCards[3]!, 2, 0);
            SetGridCell(khoCards[4]!, 2, 1);
            SetGridCell(WarehouseInventoryValueCard, 3, 0, colSpan: 3);
        }
        else
        {
            WarehouseGrid.ColumnDefinitions = new ColumnDefinitions("*,*");
            WarehouseGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto");
            SetGridCell(WarehouseTitlePanel, 0, 0, colSpan: 2);
            WarehouseTitlePanel.Margin = new Thickness(0);
            SetGridCell(khoCards[0]!, 1, 0);
            SetGridCell(khoCards[1]!, 1, 1);
            SetGridCell(khoCards[2]!, 2, 0);
            SetGridCell(khoCards[3]!, 2, 1);
            SetGridCell(khoCards[4]!, 3, 0, colSpan: 2);
            SetGridCell(WarehouseInventoryValueCard, 4, 0, colSpan: 2);
        }
    }

    private void CapNhatBieuDoVaTop10(double width)
    {
        if (ChartHeaderGrid is not null && ChartHeaderSummaryPanel is not null)
        {
            if (width >= 820)
            {
                ChartHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
                ChartHeaderGrid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(ChartHeaderSummaryPanel, 0, 1);
                ChartHeaderSummaryPanel.HorizontalAlignment = HorizontalAlignment.Right;
                SetChildrenHorizontalAlignment(ChartHeaderSummaryPanel, HorizontalAlignment.Right);
            }
            else
            {
                ChartHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*");
                ChartHeaderGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(ChartHeaderSummaryPanel, 1, 0);
                ChartHeaderSummaryPanel.HorizontalAlignment = HorizontalAlignment.Left;
                SetChildrenHorizontalAlignment(ChartHeaderSummaryPanel, HorizontalAlignment.Left);
            }
        }

        if (ChartInsightsGrid is not null &&
            InsightCard0 is not null && InsightCard1 is not null && InsightCard2 is not null)
        {
            if (width >= 820)
            {
                ChartInsightsGrid.ColumnDefinitions = new ColumnDefinitions("*,*,*");
                ChartInsightsGrid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(InsightCard0, 0, 0);
                SetGridCell(InsightCard1, 0, 1);
                SetGridCell(InsightCard2, 0, 2);
            }
            else
            {
                ChartInsightsGrid.ColumnDefinitions = new ColumnDefinitions("*");
                ChartInsightsGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
                SetGridCell(InsightCard0, 0, 0);
                SetGridCell(InsightCard1, 1, 0);
                SetGridCell(InsightCard2, 2, 0);
            }
        }

        if (Top10SectionHeaderGrid is not null && Top10SectionPeriodText is not null)
        {
            if (width >= 720)
            {
                Top10SectionHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
                Top10SectionHeaderGrid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(Top10SectionPeriodText, 0, 1);
                Top10SectionPeriodText.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                Top10SectionHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*");
                Top10SectionHeaderGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(Top10SectionPeriodText, 1, 0);
                Top10SectionPeriodText.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        if (Top10Grid is not null && TopProductsCard is not null && TopCustomersCard is not null)
        {
            if (width >= 960)
            {
                Top10Grid.ColumnDefinitions = new ColumnDefinitions("*,*");
                Top10Grid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(TopProductsCard, 0, 0);
                SetGridCell(TopCustomersCard, 0, 1);
            }
            else
            {
                Top10Grid.ColumnDefinitions = new ColumnDefinitions("*");
                Top10Grid.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(TopProductsCard, 0, 0);
                SetGridCell(TopCustomersCard, 1, 0);
            }
        }

        if (TopProductsHeaderGrid is not null && TopProductsFilterPanel is not null)
        {
            var topProductsOneRow = width >= 1140 || (width >= 660 && width < 960);
            if (topProductsOneRow)
            {
                TopProductsHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
                TopProductsHeaderGrid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(TopProductsFilterPanel, 0, 1);
                TopProductsFilterPanel.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                TopProductsHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*");
                TopProductsHeaderGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(TopProductsFilterPanel, 1, 0);
                TopProductsFilterPanel.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        if (TopCustomersHeaderGrid is not null && TopCustomersPeriodText is not null)
        {
            var topCustomersOneRow = width >= 1060 || (width >= 620 && width < 960);
            if (topCustomersOneRow)
            {
                TopCustomersHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
                TopCustomersHeaderGrid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(TopCustomersPeriodText, 0, 1);
                TopCustomersPeriodText.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                TopCustomersHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*");
                TopCustomersHeaderGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(TopCustomersPeriodText, 1, 0);
                TopCustomersPeriodText.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }
    }

    private void CapNhatNhatKyHoatDong(double width)
    {
        if (ActivityHeaderGrid is not null && ActivityHeaderCountBadge is not null)
        {
            if (width >= 760)
            {
                ActivityHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
                ActivityHeaderGrid.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(ActivityHeaderCountBadge, 0, 1);
                ActivityHeaderCountBadge.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                ActivityHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*");
                ActivityHeaderGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(ActivityHeaderCountBadge, 1, 0);
                ActivityHeaderCountBadge.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        if (ActivityFilterGrid is null ||
            ActivitySearchBox is null ||
            ActivityUserCombo is null ||
            ActivityActionCombo is null ||
            ActivityResetButton is null)
            return;

        if (width >= 920)
        {
            ActivityFilterGrid.ColumnDefinitions = new ColumnDefinitions("2*,1.2*,1.2*,Auto");
            ActivityFilterGrid.RowDefinitions = new RowDefinitions("Auto");
            SetGridCell(ActivitySearchBox, 0, 0);
            SetGridCell(ActivityUserCombo, 0, 1);
            SetGridCell(ActivityActionCombo, 0, 2);
            SetGridCell(ActivityResetButton, 0, 3);
            ActivityResetButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else if (width >= 580)
        {
            ActivityFilterGrid.ColumnDefinitions = new ColumnDefinitions("*,*,Auto");
            ActivityFilterGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            SetGridCell(ActivitySearchBox, 0, 0, colSpan: 3);
            SetGridCell(ActivityUserCombo, 1, 0);
            SetGridCell(ActivityActionCombo, 1, 1);
            SetGridCell(ActivityResetButton, 1, 2);
            ActivityResetButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            ActivityFilterGrid.ColumnDefinitions = new ColumnDefinitions("*");
            ActivityFilterGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");
            SetGridCell(ActivitySearchBox, 0, 0);
            SetGridCell(ActivityUserCombo, 1, 0);
            SetGridCell(ActivityActionCombo, 2, 0);
            SetGridCell(ActivityResetButton, 3, 0);
            ActivityResetButton.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }

    private void OnMenuMoChungTuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm
            && (sender as Control)?.DataContext is TongQuanHoatDongTaiKhoanCon item)
        {
            vm.MoChungTuHoatDongSubCommand.Execute(item);
        }
    }

    private void OnMenuSaoChepMaClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm
            && (sender as Control)?.DataContext is TongQuanHoatDongTaiKhoanCon item)
        {
            vm.SaoChepMaChungTuCommand.Execute(item);
        }
    }

    private void OnMenuSaoChepChiTietClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm
            && (sender as Control)?.DataContext is TongQuanHoatDongTaiKhoanCon item)
        {
            vm.SaoChepChiTietHoatDongCommand.Execute(item);
        }
    }

    private static void SetGridCell(Control control, int row, int column, int colSpan = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, colSpan);
    }

    private static void SetChildrenHorizontalAlignment(Panel panel, HorizontalAlignment alignment)
    {
        foreach (var child in panel.Children)
        {
            if (child is Control control)
                control.HorizontalAlignment = alignment;
        }
    }
}
