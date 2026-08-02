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

/// <summary>Origem do disparo de uma execução de sincronização.</summary>
public enum DisparoSincronizacao
{
    /// <summary>Disparada por um operador pela tela.</summary>
    Manual = 1,

    /// <summary>Disparada pelo scheduler contínuo (ADR-0024).</summary>
    Agendado = 2,
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

/// <summary>
/// Natureza de uma divergência de identidade entre a ORIGEM (PEP) e o hub FHIR, detectada
/// no momento do upsert canônico do Patient. Ver <c>docs/integracoes/plano-sincronismo-hub.md §3.2</c>.
/// </summary>
public enum TipoDivergenciaIdentidade
{
    /// <summary>Mesmo CPF, data de nascimento diferente entre origem e hub — candidato a cadastro trocado.</summary>
    NascimentoDivergente = 1,
}

/// <summary>Ciclo de vida de uma divergência de identidade.</summary>
public enum StatusDivergenciaIdentidade
{
    /// <summary>Detectada, ainda não arbitrada pela consulta externa.</summary>
    Pendente = 1,

    /// <summary>Arbitrada com veredicto conclusivo (a consulta disse qual valor é o correto).</summary>
    Verificada = 2,

    /// <summary>A consulta rodou e não foi conclusiva (motor indisponível, ou negou os dois valores).</summary>
    NaoConclusiva = 3,

    /// <summary>Um operador decidiu ignorar (falso positivo). Deixa de congelar o campo.</summary>
    Ignorada = 4,
}

/// <summary>
/// Quem está certo, segundo a consulta externa (Receita/CADSUS pelos motores de proxy CPF).
/// A semântica vem do próprio serviço: o CPF só valida quando o par CPF+nascimento confere.
/// </summary>
public enum VeredictoDivergenciaIdentidade
{
    /// <summary>Ainda não arbitrado.</summary>
    Indefinido = 0,

    /// <summary>O valor da ORIGEM validou — o hub está desatualizado/errado.</summary>
    OrigemCorreta = 1,

    /// <summary>O valor do HUB validou — a origem está errada e NÃO pode sobrescrever.</summary>
    HubCorreto = 2,

    /// <summary>Nenhum dos dois validou — o CPF em si é suspeito (provável troca de titular).</summary>
    AmbosNegados = 3,

    /// <summary>Os dois validaram (não deveria acontecer) ou o motor foi ambíguo — exige olho humano.</summary>
    Inconclusivo = 4,
}
