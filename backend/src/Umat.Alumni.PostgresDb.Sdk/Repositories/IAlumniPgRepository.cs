using Umat.Alumni.PostgresDb.Sdk.DbContexts;

namespace Umat.Alumni.PostgresDb.Sdk.Repositories;

public interface IAlumniPgRepository<T> : IPgRepository<T, AlumniDbContext> { }
