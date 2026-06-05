namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Módulos do sistema que aceitam controle de acesso. O valor inteiro
/// é estável (persistido) — não renumerar valores existentes.
/// </summary>
public enum ModuloPermissao
{
    Pacientes = 1,
    Unidades = 2,
    Veiculos = 3,
    Motoristas = 4,
    Usuarios = 5,
    TiposTratamento = 6,
    Perfis = 7,
    Tratamentos = 8,
    Translados = 9,
    Rastreamento = 10,
    Avaliacoes = 11,
    Pacs = 12,
    Medicos = 13,
    Laudos = 14,
    LaudosTemplates = 15,
    SolicitacoesExame = 16,
    TiposExame = 17,
    ProcedimentosSigtap = 18,

    /// <summary>Usar o assistente de IA (fazer perguntas em linguagem natural).</summary>
    Inteligencia = 19,

    /// <summary>Configurar o módulo IA: token do provedor e bases de dados.</summary>
    InteligenciaConfiguracao = 20,

    /// <summary>Governar o aprendizado da IA: ver o log de melhorias e remover instruções aprendidas.</summary>
    InteligenciaAprendizado = 21,

    /// <summary>Especialidades médicas (tabela de referência do agendamento).</summary>
    Especialidades = 22,

    /// <summary>Agendas dos médicos e marcação de consultas (agendamento local).</summary>
    Agendamentos = 23,

    /// <summary>Consultar o feed de leitura do SISREG (solicitações/marcações).</summary>
    Sisreg = 24,

    /// <summary>Configurar a integração SISREG: credenciais, UF/município, centrais reguladoras.</summary>
    SisregConfiguracao = 25,
}
