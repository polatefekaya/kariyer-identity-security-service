using NpgsqlTypes;

namespace Kariyer.Identity.Domain.Enums;

public enum ApprovedStatus
{
    [PgName("registered")]
    Registered,

    [PgName("inserted")]
    Inserted,

    [PgName("none")]
    None
}
