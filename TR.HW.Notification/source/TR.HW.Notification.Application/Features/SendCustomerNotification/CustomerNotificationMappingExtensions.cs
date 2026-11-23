using TR.HW.Notification.Domain.Entities;
using TR.HW.Notification.Domain.Enums;

namespace TR.HW.Notification.Application.Features.SendCustomerNotification;

public static class CustomerNotificationMappingExtensions
{
    public static (string Title, string Content) FormatMessage(NotificationType type, NotificationChannel channel, Guid reservationId, string? detailsJson = null)
    {
        return type switch
        {
            NotificationType.TicketIssuedConfirmation => channel switch
            {
                NotificationChannel.Email => (
                    "Seus Ingressos Estão Prontos! 🎟️",
                    $"Olá! Sua reserva {reservationId} foi confirmada com sucesso e seus ingressos estão disponíveis para acesso no app."
                ),
                NotificationChannel.Push => (
                    "Ingressos Confirmados!",
                    $"Sua reserva {reservationId} foi concluída. Toque para ver seus ingressos."
                ),
                _ => ("Confirmação de Ingressos", $"Reserva {reservationId} confirmada.")
            },
            NotificationType.PaymentRefundedNotice => channel switch
            {
                NotificationChannel.Email => (
                    "Aviso de Estorno Automático Realizado",
                    $"Informamos que a reserva {reservationId} não pôde ser emitida devido à concorrência de assentos. O estorno integral já foi processado no seu cartão."
                ),
                NotificationChannel.Push => (
                    "Estorno Realizado",
                    $"A reserva {reservationId} foi cancelada e seu pagamento estornado integralmente."
                ),
                _ => ("Aviso de Estorno", $"Estorno processado para reserva {reservationId}.")
            },
            NotificationType.PaymentDeclinedNotice => channel switch
            {
                NotificationChannel.Email => (
                    "Pagamento Não Autorizado para sua Reserva",
                    $"A tentativa de cobrança para a reserva {reservationId} foi recusada pelo banco/gateway. A reserva foi cancelada e os assentos liberados."
                ),
                NotificationChannel.Push => (
                    "Pagamento Recusado",
                    $"Não conseguimos aprovar o pagamento da reserva {reservationId}. Tente novamente com outro cartão."
                ),
                _ => ("Pagamento Recusado", $"Cobrança recusada para reserva {reservationId}.")
            },
            _ => ("Notificação TicketReserver", $"Atualização da reserva {reservationId}.")
        };
    }

    public static List<CustomerNotification> BuildNotifications(
        SendCustomerNotificationCommand command,
        DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var notifications = new List<CustomerNotification>();

        // 1. Canal Email (Mandatório se email estiver presente)
        var (emailTitle, emailContent) = FormatMessage(command.NotificationType, NotificationChannel.Email, command.ReservationId, command.DetailsJson);
        notifications.Add(CustomerNotification.Create(
            Guid.NewGuid(),
            command.ReservationId,
            command.CustomerId,
            command.CustomerEmail,
            command.NotificationType,
            NotificationChannel.Email,
            emailTitle,
            emailContent,
            command.DetailsJson,
            timestamp));

        // 2. Canal Push (Opcional, quando token push estiver disponível)
        if (!string.IsNullOrWhiteSpace(command.CustomerPushToken))
        {
            var (pushTitle, pushContent) = FormatMessage(command.NotificationType, NotificationChannel.Push, command.ReservationId, command.DetailsJson);
            notifications.Add(CustomerNotification.Create(
                Guid.NewGuid(),
                command.ReservationId,
                command.CustomerId,
                command.CustomerPushToken,
                command.NotificationType,
                NotificationChannel.Push,
                pushTitle,
                pushContent,
                command.DetailsJson,
                timestamp));
        }

        return notifications;
    }
}
