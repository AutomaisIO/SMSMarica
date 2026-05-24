namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Estado do laudo no fluxo de emissão. Após Finalizado o laudo é imutável —
/// nova edição cria uma nova versão (Versao + 1, LaudoAnteriorId apontando
/// para a anterior).
/// </summary>
public enum StatusLaudo
{
    Rascunho = 1,
    Finalizado = 2,
}
