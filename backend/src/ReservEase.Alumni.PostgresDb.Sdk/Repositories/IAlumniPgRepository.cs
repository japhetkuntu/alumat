using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Repositories;

public interface IAlumniPgRepository<T> : IPgRepository<T, AlumniDbContext> where T : BaseEntity { }
