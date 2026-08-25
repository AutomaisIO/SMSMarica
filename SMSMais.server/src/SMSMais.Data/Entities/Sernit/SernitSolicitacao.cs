namespace SMSMais.Data.Entities.Sernit;

/// <summary>Situação da solicitação no SERNIT (SER de Niterói). Os nomes espelham os values do
/// combo de Situação da tela de pesquisa (medido em <c>form0:j_id54</c>, id volátil — resolver
/// pelo <c>&lt;select&gt;</c> que oferece <c>EM_FILA</c>). Mudar aqui quebra a varredura.</summary>
public enum SituacaoSernit
{
    EmFila = 1,
    Pendente = 2,
    Agendada = 3,
    ChegadaNaoConfirmada = 4,
    ChegadaConfirmada = 5,
    Cancelada = 6,
    Alta = 7,
}

/// <summary>Tipo do recurso solicitado no SERNIT.</summary>
public enum TipoRecursoSernit
{
    Consulta = 1,
    Exame = 2,
}

/// <summary>
/// Espelho fiel de uma solicitação do SERNIT (SER de Niterói) — subsistema irmão do SER-RJ
/// (ADR-0042), em tabelas próprias <c>sernit_*</c> para isolar do SER-RJ em produção.
///
/// <para><b>Não é uma <see cref="Solicitacao"/></b> (mesma razão do SER-RJ: sem unidade executante
/// em Maricá, sem SIGTAP, vocabulário de situação próprio). Promoção a <c>solicitacao</c> fica
/// para quando a solicitação virar acionável.</para>
///
/// <para><b>Uma tabela para todas as situações</b> — o que varia é <see cref="Situacao"/>.</para>
///
/// <para>Os campos de <c>Paciente*</c>/<c>Telefone*</c>/endereço vêm da tela de HISTÓRICO, não da
/// grade — ficam nulos até o histórico ser lido pela primeira vez.</para>
/// </summary>
public class SernitSolicitacao
{
    public Guid Id { get; set; }

    /// <summary><b>Chave natural: o "ID Solicitação" do SERNIT.</b> Índice único (por fonte, aqui
    /// implícita na tabela) — torna a re-varredura idempotente. Numérico CURTO no SERNIT
    /// ("1742", "9891"), guardado como texto por ser identificador de sistema externo. Não colide
    /// com os IDs de 7 dígitos do SER-RJ porque vive em tabela separada.</summary>
    public string IdSernit { get; set; } = string.Empty;

    // ---- Colunas da grade "Solicitações de Consulta ou Exame" ----

    public TipoRecursoSernit Tipo { get; set; }

    /// <summary>Procedimento como o SERNIT escreve, em TEXTO (não há código SIGTAP).</summary>
    public string Recurso { get; set; } = string.Empty;

    public DateOnly? DataSolicitacao { get; set; }

    public string PacienteNome { get; set; } = string.Empty;

    /// <summary>Idade como o SERNIT exibe ("15 ano(s), 7 meses e 7 dia(s)."). Texto — é calculada
    /// na origem e serve de conferência contra a data de nascimento.</summary>
    public string? IdadeTexto { get; set; }

    /// <summary>CPF só dígitos. Pode faltar (muitos pacientes sem CPF na fila do SERNIT).</summary>
    public string? Cpf { get; set; }

    public string? Cns { get; set; }

    /// <summary>CID como veio ("J353 - Hipertrofia das amígdalas...").</summary>
    public string? Cid { get; set; }

    /// <summary>A grade do SERNIT NÃO traz solicitante/município (a conta já é escopada a Maricá).
    /// Mantido para paridade com o SER-RJ; fica nulo lido pela grade.</summary>
    public string? SolicitanteNome { get; set; }

    public string? MunicipioSolicitante { get; set; }

    /// <summary>Unidade onde o paciente vai ser atendido, texto do SERNIT. Pode faltar na grade.</summary>
    public string? UnidadeExecutora { get; set; }

    /// <summary>Coluna "Agendado para" da grade. Texto porque o SERNIT mistura formatos.</summary>
    public string? AgendadoParaTexto { get; set; }

    public SituacaoSernit Situacao { get; set; }

    // ---- Dados do paciente (só existem na tela de HISTÓRICO) ----

    public string? NomeMae { get; set; }
    public string? Sexo { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public string? Etnia { get; set; }
    public string? Cep { get; set; }
    public string? Uf { get; set; }
    public string? MunicipioPaciente { get; set; }
    public string? Bairro { get; set; }
    public string? TipoLogradouro { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? TelefoneResidencial { get; set; }

    /// <summary>Campo de notificação do SERNIT — rotulado <b>"Telefone SMS"</b> na tela de
    /// Histórico e <b>"Telefone Celular"</b> na de Editar. Equivale ao "WhatsApp" do SER-RJ;
    /// insumo direto para o hub FHIR e para a comunicação com o paciente.</summary>
    public string? TelefoneWhatsapp { get; set; }

    public string? TelefoneContato { get; set; }

    // ---- Controle da sincronização ----

    /// <summary>Id do paciente NO NOSSO HUB FHIR, quando a conciliação resolveu quem é. Sem FK
    /// (o paciente vive no serviço FHIR autônomo, ADR-0010).</summary>
    public Guid? PacienteId { get; set; }

    /// <summary>Quando o dado de PACIENTE mudou e ainda não foi levado ao hub FHIR. A varredura só
    /// carimba; o worker de conciliação leva ao hub no seu ritmo (não derruba a rodada se o hub
    /// cair).</summary>
    public DateTime? PacienteConciliarEm { get; set; }

    public DateTime SincronizadoEm { get; set; }

    /// <summary>Quando o HISTÓRICO foi lido pela última vez. Null = nunca leu.</summary>
    public DateTime? HistoricoLidoEm { get; set; }

    /// <summary>Qtde de eventos na última leitura — atalho para detectar evento novo.</summary>
    public int EventosCount { get; set; }

    public DateTime? UltimoEventoEm { get; set; }

    /// <summary>O menu Opções não oferece "Histórico da Solicitação" (situação
    /// <see cref="SituacaoSernit.Alta"/>). Marcado para o motor não reler todo dia.</summary>
    public bool HistoricoIndisponivel { get; set; }

    public DateTime? SituacaoMudouEm { get; set; }
    public SituacaoSernit? SituacaoAnterior { get; set; }

    // ---- Auditoria (ADR-0006) ----

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    public ICollection<SernitEvento> Eventos { get; set; } = [];
}
