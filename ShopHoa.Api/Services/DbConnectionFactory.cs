using Dapper;
using MySqlConnector;

namespace ShopHoa.Api.Services;

public interface IDbConnectionFactory
{
    Task<MySqlConnection> CreateConnectionAsync();
}

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private static bool? _hasPhieuQuyLoaiPhieuColumn;
    private static readonly SemaphoreSlim KhoaEnsureLoaiPhieu = new(1, 1);
    private static bool? _hasHoaDonDebtTrackingColumn;
    private static readonly SemaphoreSlim KhoaEnsureHoaDonDebtTracking = new(1, 1);

    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Chuỗi kết nối 'DefaultConnection' không được tìm thấy trong cấu hình.");
    }

    public async Task<MySqlConnection> CreateConnectionAsync()
    {
        await EnsurePhieuQuyLoaiPhieuColumnAsync();
        await EnsureHoaDonDebtTrackingColumnAsync();

        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    /// Bảo đảm cột <c>phieu_chi.loai_phieu</c> tồn tại trước khi báo cáo chạy.
    /// Phiếu nhập từ file có thể mang mã TT/CTM... nên không thể chỉ suy loại từ PT/PC.
    /// </summary>
    private async Task EnsurePhieuQuyLoaiPhieuColumnAsync()
    {
        if (_hasPhieuQuyLoaiPhieuColumn.HasValue)
            return;

        await KhoaEnsureLoaiPhieu.WaitAsync();

        try
        {
            if (_hasPhieuQuyLoaiPhieuColumn.HasValue)
                return;

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            const string kiemTraSql = """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'phieu_chi'
              AND COLUMN_NAME = 'loai_phieu';
            """;

            var count = await connection.ExecuteScalarAsync<long>(kiemTraSql);

            if (count == 0)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE phieu_chi ADD COLUMN loai_phieu varchar(10) DEFAULT NULL AFTER ma_phieu_chi;");
            }

            _hasPhieuQuyLoaiPhieuColumn = true;
        }
        catch (MySqlException ex) when (ex.Number == 1060)
        {
            _hasPhieuQuyLoaiPhieuColumn = true;
        }
        catch
        {
            _hasPhieuQuyLoaiPhieuColumn = false;
        }
        finally
        {
            KhoaEnsureLoaiPhieu.Release();
        }
    }

    private async Task EnsureHoaDonDebtTrackingColumnAsync()
    {
        if (_hasHoaDonDebtTrackingColumn == true)
            return;

        await KhoaEnsureHoaDonDebtTracking.WaitAsync();
        try
        {
            if (_hasHoaDonDebtTrackingColumn == true)
                return;

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var count = await connection.ExecuteScalarAsync<long>(
                """
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'hoa_don_ban_hang'
                  AND COLUMN_NAME = 'cong_no_da_ghi_nhan';
                """);

            if (count == 0)
            {
                try
                {
                    await connection.ExecuteAsync(
                        "ALTER TABLE hoa_don_ban_hang "
                        + "ADD COLUMN cong_no_da_ghi_nhan TINYINT(1) NOT NULL DEFAULT 1 AFTER con_no;");
                }
                catch (MySqlException ex) when (ex.Number == 1060)
                {
                    var columnExists = await connection.ExecuteScalarAsync<long>(
                        """
                        SELECT COUNT(*)
                        FROM information_schema.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE()
                          AND TABLE_NAME = 'hoa_don_ban_hang'
                          AND COLUMN_NAME = 'cong_no_da_ghi_nhan';
                        """);
                    if (columnExists == 0)
                        throw;
                }
            }

            _hasHoaDonDebtTrackingColumn = true;
        }
        finally
        {
            KhoaEnsureHoaDonDebtTracking.Release();
        }
    }
}
