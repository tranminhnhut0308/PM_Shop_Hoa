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

    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Chuỗi kết nối 'DefaultConnection' không được tìm thấy trong cấu hình.");
    }

    public async Task<MySqlConnection> CreateConnectionAsync()
    {
        await EnsurePhieuQuyLoaiPhieuColumnAsync();

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
}
