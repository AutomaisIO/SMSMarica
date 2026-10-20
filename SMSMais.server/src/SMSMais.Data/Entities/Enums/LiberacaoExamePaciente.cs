namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Quando o exame de um <see cref="Equipamento"/> chega ao paciente — app do cidadão e WhatsApp
/// "Exame liberado" (ADR-0075). Persistido como int — valores estáveis.
/// </summary>
public enum LiberacaoExamePaciente
{
    /// <summary>
    /// O exame fica retido e sai junto com o laudo assinado, numa mensagem só ("Laudo pronto"). É o
    /// padrão: a médica revê a imagem depois do exame e ela pode mudar (estudo refeito, imagem
    /// rejeitada) — mandar antes é entregar ao paciente o que ainda não está fechado. Só vale com
    /// <see cref="OrigemLaudo.Plataforma"/>: com o laudo feito fora, o exame ficaria preso para sempre.
    /// </summary>
    ComLaudoAssinado = 1,

    /// <summary>Libera assim que o estudo chega (ultrassom, laudado na hora em outra plataforma; MAPA e
    /// Holter, que chegam com o laudo dentro).</summary>
    AoChegar = 2,
}
