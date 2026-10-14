namespace ApiToMcpServer.Persistence;

public static class SqlQueries
{
    public const string CreateStatusesTableSql = """
    CREATE TABLE IF NOT EXISTS Statuses (
        Id INTEGER PRIMARY KEY,
        Name TEXT NOT NULL UNIQUE
    );
    """;
    public const string InsertStatusesSql = """
    INSERT OR IGNORE INTO Statuses (Id, Name)
    VALUES
        (1, 'Pending'),
        (2, 'Approved'),
        (3, 'Rejected'),
        (4, 'Expired');
    """;
    public const string CreateApprovalsTableSql = """
    CREATE TABLE IF NOT EXISTS Approvals (
        Id TEXT PRIMARY KEY,
        Operation TEXT NOT NULL,
        StatusId INTEGER NOT NULL,
        CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL,
        ExpiresAt TEXT NOT NULL,
        CreatedBy INTEGER NOT NULL,
        UpdatedBy INTEGER NULL,
        FOREIGN KEY (StatusId) REFERENCES Statuses(Id),
        FOREIGN KEY (CreatedBy) REFERENCES Users(Id),
        FOREIGN KEY (UpdatedBy) REFERENCES Users(Id)
    );
    """;
    public const string InsertApprovalSql = """
    INSERT INTO Approvals
        (Id, Operation, StatusId, CreatedAt, UpdatedAt, ExpiresAt, CreatedBy, UpdatedBy)
    VALUES
        (@Id, @Operation, @StatusId, @CreatedAt, @UpdatedAt, @ExpiresAt, @CreatedBy, @UpdatedBy);
    """
    ;
    public const string UpdateStatusSql = """
    UPDATE Approvals
    SET StatusId = @StatusId,
        UpdatedAt = @UpdatedAt,
        UpdatedBy = @UpdatedBy
    WHERE Id = @Id
      AND StatusId = @PendingStatusId
      AND @UpdatedAt < ExpiresAt;
    """;
    public const string DeleteApprovalSql = """
    DELETE FROM Approvals WHERE Id = @Id;
    """;
    // StatusId aliased to Status so Dapper maps it onto the ApprovalStatus enum
    public const string GetApprovalByIdSql = """
    SELECT Id, Operation, StatusId AS Status, CreatedAt, UpdatedAt, ExpiresAt, CreatedBy, UpdatedBy
    FROM Approvals
    WHERE Id = @Id;
    """;
    public const string GetAllApprovalsSql = """
    SELECT Id, Operation, StatusId AS Status, CreatedAt, UpdatedAt, ExpiresAt, CreatedBy, UpdatedBy
    FROM Approvals;
    """;

    public const string CreateUsersTableSql = """
    CREATE TABLE IF NOT EXISTS Users (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        FirstName TEXT NOT NULL,
        LastName TEXT NOT NULL,
        Email TEXT NOT NULL UNIQUE,
        WorkOSId TEXT NOT NULL UNIQUE
    );
    """;
    public const string InsertUserSql = """
    INSERT INTO Users (FirstName, LastName, Email, WorkOSId)
    VALUES ( @FirstName, @LastName, @Email, @WorkOSId)
    RETURNING Id;
    """;

    public const string GetUserByEmailSql = """
    SELECT Id, FirstName, LastName, Email, WorkOSId
    FROM Users
    WHERE Email = @Email;
    """;

    public const string GetUserByWorkOSIdSql = """
    SELECT Id, FirstName, LastName, Email, WorkOSId
    FROM Users
    WHERE WorkOSId = @WorkOSId;
    """;
}
