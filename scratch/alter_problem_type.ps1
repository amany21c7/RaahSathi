$connString = "Server=AmanYadav-PC\SQLEXPRESS;Database=RaahSathiDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
$connection = New-Object System.Data.SqlClient.SqlConnection($connString)
$connection.Open()

$sql = @"
ALTER TABLE [Jobs] ALTER COLUMN [ProblemType] NVARCHAR(MAX) NOT NULL;
"@

$command = $connection.CreateCommand()
$command.CommandText = $sql
$res = $command.ExecuteNonQuery()
Write-Host "Successfully altered [ProblemType] column to NVARCHAR(MAX)! Result: $res"
$connection.Close()
