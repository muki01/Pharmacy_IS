using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;

namespace PharmacyIS.Data.Repositories;

/// <summary>Достъп до одитния дневник на системата.</summary>
public class AuditRepository
{
    private readonly PharmacyDb _db;

    public AuditRepository(PharmacyDb db) => _db = db;

    /// <summary>Записва събитие в одитния дневник.</summary>
    public void Log(string username, string action, bool isSuccess, string? details = null)
    {
        _db.Execute("""
            INSERT INTO AuditLog (EventDate, Username, Action, Details, IsSuccess)
            VALUES (@date, @username, @action, @details, @success);
            """,
            ("@date", DateTime.Now),
            ("@username", username),
            ("@action", action),
            ("@details", details),
            ("@success", isSuccess));
    }

    /// <summary>Последните записи от дневника.</summary>
    public List<AuditLogEntry> GetRecent(int count = 200) => _db.Query($"""
        SELECT LogId, EventDate, Username, Action, Details, IsSuccess
        FROM AuditLog
        ORDER BY EventDate DESC, LogId DESC
        LIMIT {count};
        """,
        (DbDataReader r) => new AuditLogEntry
        {
            LogId = r.GetInt("LogId"),
            EventDate = r.GetDateTime("EventDate"),
            Username = r.GetString("Username"),
            Action = r.GetString("Action"),
            Details = r.GetNullableString("Details"),
            IsSuccess = r.GetBool("IsSuccess")
        });

    /// <summary>
    /// Брой неуспешни опити за вход с дадено потребителско име през последните минути.
    /// Използва се за временно блокиране при атака с познаване на паролата.
    /// </summary>
    public int CountRecentFailedLogins(string username, int withinMinutes)
    {
        return Convert.ToInt32(_db.ExecuteScalar("""
            SELECT COUNT(*)
            FROM AuditLog
            WHERE Username = @username
              AND Action = 'LOGIN_FAILED'
              AND EventDate >= @since;
            """,
            ("@username", username),
            ("@since", DateTime.Now.AddMinutes(-withinMinutes))));
    }
}
