namespace SMSMarica.Data.Entities.Ser;

/// <summary>Situação da solicitação no SER. Os nomes espelham os values do combo
/// <c>form0:j_id75</c> da tela de pesquisa — mudar aqui quebra a varredura.</summary>
public enum SituacaoSer
{
    EmFila = 1,
    Pendente = 2,
    Agendada = 3,
    ChegadaNaoConfirmada = 4,
    ChegadaConfirmada = 5,
    Cancelada = 6,
    Alta = 7,
}

/// <summary>Tipo do recurso solicitado no SER.</summary>
public enum TipoRecursoSer
{
    Consulta = 1,
    Exame = 2,
}

/// <summary>
/// Espelho fiel de uma solicitação do SER (SES-RJ) — ADR-0042.
///
/// <para><b>Não é uma <see cref="Solicitacao"/>.</b> É deliberadamente uma tabela separada: a
/// solicitação do SER não tem unidade executante em Maricá (quem executa é o Estado), não traz
/// código SIGTAP (o "Recurso" vem em texto) e usa outro vocabulário de situação. Misturar as
/// duas contaminaria worklist, recepção, gate do PACS e indicadores com ~5.000 linhas que o
/// município não executa. A promoção para <c>solicitacao</c> acontece quando (e se) a
/// solicitação virar acionável — agendada, avisar o cidadão, TFD.</para>
///
/// <para><b>Uma tabela para todas as situações</b> — o que varia é <see cref="Situacao"/>. A
/// varredura roda uma passada por situação só porque o SER exige o filtro na busca; o destino
/// é o mesmo.</para>
///
/// <para>Os campos de <c>Paciente*</c>/<c>Telefone*</c>/endereço vêm da tela de HISTÓRICO, não
/// da grade — ficam nulos até o histórico ser lido pela primeira vez.</para>
/// </summary>
public class SerSolicitacao
{
    public Guid Id { get; set; }

    /// <summary><b>Chave natural: o "ID Solicitação" do SER.</b> Índice único — é o que torna a
    /// re-varredura idempotente. Numérico de ~7 dígitos ("3968616"), guardado como texto porque
    /// é identificador de sistema externo, não número de contar.</summary>
    public string IdSer { get; set; } = string.Empty;

    // ---- Colunas da grade "Solicitações de Consulta ou Exame" ----

    public TipoRecursoSer Tipo { get; set; }

    /// <summary>Procedimento como o SER escreve, em TEXTO. Não há código SIGTAP no SER
    /// ("CONSULTA EM OFTALMOLOGIA - PLASTICA OCULAR").</summary>
    public string Recurso { get; set; } = string.Empty;

    public DateOnly? DataSolicitacao { get; set; }

    public string PacienteNome { get; set; } = string.Empty;

    /// <summary>Idade como o SER exibe ("69 ano(s), 10 meses e 19 dia(s)."). Texto porque é
    /// calculada na origem e serve de conferência contra a data de nascimento.</summary>
    public string? IdadeTexto { get; set; }

    /// <summary>CPF só dígitos (a grade traz formatado). Pode faltar.</summary>
    public string? Cpf { get; set; }

    public string? Cns { get; set; }

    /// <summary>CID como veio ("S05 - Traumatismo do olho e da órbita ocular").</summary>
    public string? Cid { get; set; }

    public string? SolicitanteNome { get; set; }

    public string? MunicipioSolicitante { get; set; }

    /// <summary>
    /// Unidade onde o paciente vai ser atendido, como texto do SER.
    ///
    /// <para>Só a tela de <b>Histórico de Consulta/Exame</b> (a do export) traz essa coluna — a
    /// grade da tela de Solicitação não tem. Fica nulo para o que foi lido por lá, inclusive ALTA.</para>
    /// </summary>
    public string? UnidadeExecutora { get; set; }

    /// <summary>Coluna "Agendado para" da grade. Texto porque o SER mistura formatos
    /// (data, data+hora, data+hora+unidade) conforme a situação.</summary>
    public string? AgendadoParaTexto { get; set; }

    public SituacaoSer Situacao { get; set; }

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

    /// <summary>O SER tem campo próprio de WhatsApp. Insumo direto para o hub FHIR e para a
    /// comunicação com o paciente.</summary>
    public string? TelefoneWhatsapp { get; set; }

    public string? TelefoneContato { get; set; }

    // ---- Controle da sincronização ----

    /// <summary>Quando a grade foi lida pela última vez (toda varredura atualiza).</summary>
    /// <summary>
    /// Quando o dado de PACIENTE desta solicitação mudou e ainda não foi levado ao hub FHIR.
    /// <c>null</c> = nada pendente.
    ///
    /// <para><b>Por que uma marca, e não uma chamada ao hub dentro da varredura.</b> A rodada
    /// completa leva ~7 horas e passa por 25 mil solicitações; pendurar nela uma ida ao hub por
    /// paciente somaria milhares de requisições e — o que pesa de verdade — faria uma
    /// indisponibilidade do hub derrubar a varredura inteira. Aqui a varredura só carimba quem
    /// mudou; o worker de conciliação leva ao hub no seu ritmo, e se o hub estiver fora a marca
    /// continua esperando.</para>
    /// </summary>
    public DateTime? PacienteConciliarEm { get; set; }

    public DateTime SincronizadoEm { get; set; }

    /// <summary>Quando o HISTÓRICO foi lido pela última vez. Null = nunca leu.</summary>
    public DateTime? HistoricoLidoEm { get; set; }

    /// <summary>Quantidade de eventos na última leitura do histórico — atalho para detectar
    /// evento novo sem carregar a trilha inteira.</summary>
    public int EventosCount { get; set; }

    /// <summary>Data do evento mais recente conhecido. É o corte do diff de histórico.</summary>
    public DateTime? UltimoEventoEm { get; set; }

    /// <summary>
    /// O SER não oferece "Histórico da Solicitação" no menu Opções desta linha (acontece com
    /// situação <see cref="SituacaoSer.Alta"/>). Marcado explicitamente para o motor não ficar
    /// tentando reler todo dia — e para a tela poder explicar a ausência ao operador.
    /// </summary>
    public bool HistoricoIndisponivel { get; set; }

    /// <summary>Quando a situação mudou pela última vez (base do "o que mudou hoje").</summary>
    public DateTime? SituacaoMudouEm { get; set; }

    /// <summary>Situação anterior, para a tela mostrar a transição sem abrir o histórico.</summary>
    public SituacaoSer? SituacaoAnterior { get; set; }

    // ---- Auditoria (ADR-0006) ----

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    public ICollection<SerEvento> Eventos { get; set; } = [];
}
