namespace SMSMarica.Data.Entities.Enums;

/// <summary>Estado de uma mensagem WhatsApp (TFD). Valor inteiro estável.</summary>
public enum StatusMensagemWhatsApp
{
    Enviada = 1,
    Entregue = 2,
    Lida = 3,
    Falha = 4,
    Recebida = 5,
}
