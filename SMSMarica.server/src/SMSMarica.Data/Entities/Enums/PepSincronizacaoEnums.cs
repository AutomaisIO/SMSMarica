namespace SMSMarica.Data.Entities.Enums;

/// <summary>Modo de importação de uma base de PEP para o hub FHIR.</summary>
public enum ModoSincronizacao
{
    /// <summary>Importa do zero (respeitando o escopo). Pode purgar antes.</summary>
    Completo = 1,

    /// <summary>Importa apenas registros criados/editados desde o último sincronismo bem-sucedido.</summary>
    Incremental = 2,
}

/// <summary>Abrangência da importação completa.</summary>
public enum EscopoSincronizacao
{
    /// <summary>Limita por quantidade (N médicos / N pacientes) — fase de teste.</summary>
    Limitado = 1,

    /// <summary>Traz tudo da base.</summary>
    Tudo = 2,
}

/// <summary>Estado de uma execução de sincronização de PEP.</summary>
public enum StatusSincronizacao
{
    Pendente = 1,
    EmExecucao = 2,
    Concluido = 3,
    Erro = 4,

    /// <summary>Interrompida pelo usuário (botão "parar"). No modo Completo, o cursor de retomada fica salvo.</summary>
    Cancelado = 5,
}
