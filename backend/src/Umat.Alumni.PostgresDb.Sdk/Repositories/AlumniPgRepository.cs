using Umat.Alumni.PostgresDb.Sdk.DbContexts;

namespace Umat.Alumni.PostgresDb.Sdk.Repositories;

public class AlumniPgRepository<T>(AlumniDbContext context) : PgRepository<T, AlumniDbContext>(context), IAlumniPgRepository<T>
    where T : class { }
