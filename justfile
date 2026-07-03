build:
    dotnet build src/Migrate4to5.sln

test:
    dotnet test src/DumpFormat.Tests
    dotnet test src/Importer.Tests

# Windows only (Esent / .NET Framework)
test-windows:
    dotnet test src/Exporter.Tests
