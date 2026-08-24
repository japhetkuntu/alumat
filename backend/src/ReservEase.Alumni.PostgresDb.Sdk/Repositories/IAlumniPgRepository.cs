using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;

namespace ReservEase.Alumni.PostgresDb.Sdk.Repositories;

public interface IAlumniPgRepository<T> : IPgRepository<T, AlumniDbContext> { }
