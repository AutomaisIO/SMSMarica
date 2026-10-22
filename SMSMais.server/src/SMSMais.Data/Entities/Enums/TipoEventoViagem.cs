namespace SMSMais.Data.Entities.Enums;

/// <summary>O que o tablet do veículo registrou durante o translado.</summary>
public enum TipoEventoViagem
{
    /// <summary>O motorista conferido começou a rota.</summary>
    Inicio = 1,

    /// <summary>Paciente ou acompanhante entrou no veículo na coleta.</summary>
    Embarque = 2,

    /// <summary>Paciente ou acompanhante não estava na coleta.</summary>
    Ausencia = 3,

    /// <summary>Paciente ou acompanhante desceu no destino.</summary>
    Desembarque = 4,

    /// <summary>A última parada foi resolvida: a rota acabou.</summary>
    Conclusao = 5,
}
