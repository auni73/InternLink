using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InternLink.Web.Helpers;

public static class DbExceptionMapper
{
    public static bool IsUniqueConstraintViolation(Exception ex)
    {
        if (ex is DbUpdateException dbEx && dbEx.InnerException is SqlException sqlEx)
        {
            return sqlEx.Number is 2601 or 2627;
        }

        if (ex is SqlException directSqlEx)
        {
            return directSqlEx.Number is 2601 or 2627;
        }

        return false;
    }

    public static bool IsForeignKeyOrCheckViolation(Exception ex)
    {
        if (ex is DbUpdateException dbEx && dbEx.InnerException is SqlException sqlEx)
        {
            return sqlEx.Number is 547;
        }

        if (ex is SqlException directSqlEx)
        {
            return directSqlEx.Number is 547;
        }

        return false;
    }
}
