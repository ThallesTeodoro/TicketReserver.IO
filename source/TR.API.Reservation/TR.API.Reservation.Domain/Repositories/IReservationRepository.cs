using TR.API.Reservation.Domain.Entities;

namespace TR.API.Reservation.Domain.Repositories;

/// <summary>
/// Repositório para persistência e consulta de reservas.
/// </summary>
public interface IReservationRepository
{
    /// <summary>
    /// Adiciona uma nova reserva ao banco de dados.
    /// </summary>
    /// <param name="reservation">Entidade de reserva a ser persistida.</param>
    Task AddAsync(Entities.Reservation reservation);

    /// <summary>
    /// Obtém uma reserva pelo seu identificador único.
    /// </summary>
    /// <param name="id">GUID da reserva.</param>
    /// <returns>A entidade de reserva se encontrada; caso contrário, null.</returns>
    Task<Entities.Reservation?> GetByIdAsync(Guid id);

    /// <summary>
    /// Atualiza o estado de uma reserva existente.
    /// </summary>
    /// <param name="reservation">Entidade de reserva com os dados atualizados.</param>
    Task UpdateAsync(Entities.Reservation reservation);
}
