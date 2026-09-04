using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Uma linha da grade de escalas do SISREG (tela <c>cons_escalas</c>) — a <b>OFERTA</b> de vagas.
///
/// <para><b>O que uma linha é:</b> um bloco recorrente semanal — "toda SEX, das 11:10 às 12:00, no
/// CNES 3132358, profissional X, procedimento 0229000, 10 vagas de primeira vez" — válido entre
/// <see cref="VigenciaInicio"/> e <see cref="VigenciaFim"/>. <b>Não tem paciente</b>: quem ocupa a
/// vaga é a <c>Solicitacao</c>, e o encontro das duas é que produz "ofertado × ocupado × livre".</para>
///
/// <para><b>Onde ela se encaixa:</b> não é um mundo novo — é a camada de horário e vaga que faltava
/// pendurar na tripla que já existia (<see cref="SisregProfissionalUnidade"/> ×
/// <see cref="SisregProcedimentoProfissional"/>). Medido em 04/09/2026 contra o cadastro real:
/// 34 de 34 unidades por CNES, 198 de 199 CPFs, 118 de 119 procedimentos e 256 de 258 pares já
/// existiam.</para>
///
/// <para><b>Dois padrões de vigência, um só modelo:</b> a maioria é faixa de datas (recorrente
/// semanal), mas 6.372 linhas do arquivo têm <c>VigenciaInicio == VigenciaFim</c> — escala de um dia
/// só. Não precisa de ramo especial: é o caso degenerado da expansão (uma ocorrência). O
/// <see cref="DiaSemana"/> sempre bate com o weekday da data nesse caso — zero divergências em
/// 17.469 linhas.</para>
///
/// <para><b>Vários blocos no mesmo dia são normais</b> — até 9 para a mesma tupla, e medidos <b>sem
/// nenhuma sobreposição</b> (0 em 254 tuplas). Sobreposição, se aparecer, é sinal de dado ruim no
/// SISREG, não de modelagem errada aqui.</para>
/// </summary>
public class SisregEscala
{
    public Guid Id { get; set; }

    /// <summary>
    /// <c>COD. ESCALA AMBULATORIAL</c> — <b>chave natural e eixo de idempotência do sincronismo</b>.
    /// Único no arquivo inteiro (17.469 códigos em 17.469 linhas).
    ///
    /// <para>O SISREG edita a linha <b>in place</b>: 59% das linhas têm data de alteração diferente
    /// da de inserção, com o mesmo código. Por isso o sincronismo é <b>upsert por este campo</b>, e
    /// não append — versionar aqui criaria duplicata de oferta e dobraria a contagem de vagas.</para>
    /// </summary>
    public string CodigoEscala { get; set; } = string.Empty;

    // ---- Onde ----

    /// <summary>Unidade executante, resolvida pelo CNES do arquivo.</summary>
    public Guid UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    /// <summary>CNES do executante — snapshot, para a linha continuar legível se o cadastro mudar.</summary>
    public string Cnes { get; set; } = string.Empty;

    /// <summary>Nome da unidade como o SISREG a escreve.</summary>
    public string UnidadeNomeSisreg { get; set; } = string.Empty;

    // ---- Quem ----

    /// <summary>CPF do profissional executante (só dígitos). Casa com
    /// <see cref="SisregProfissionalUnidade.Cpf"/> e com <c>Solicitacao.ProfissionalExecutanteCpf</c>.</summary>
    public string ProfissionalCpf { get; set; } = string.Empty;

    public string ProfissionalNome { get; set; } = string.Empty;

    /// <summary>
    /// CBO do profissional — é o que serve de <b>especialidade</b> na tela de Agenda.
    ///
    /// <para>Preenchido em 96% das escalas vigentes (o SISREG escreve <c>---</c> quando não tem).
    /// Usar o CBO evita ressuscitar uma tabela local de especialidade que nunca teve uso real.</para>
    /// </summary>
    public string? CboCodigo { get; set; }
    public string? CboDescricao { get; set; }

    // ---- O quê ----

    /// <summary>Código do procedimento no SISREG (o <c>pa</c>).</summary>
    public string ProcedimentoCodigo { get; set; } = string.Empty;

    public string ProcedimentoNome { get; set; } = string.Empty;

    /// <summary>
    /// SIGTAP quando o SISREG informa. <b>Não dá para depender dele:</b> vem vazio (<c>---</c>) em
    /// ~5.400 das 17.469 linhas. Quem identifica o procedimento é o código interno + o nome.
    /// </summary>
    public string? ProcedimentoSigtap { get; set; }

    /// <summary>
    /// Código termina em <c>000</c> = é GRUPO, e expande em itens individuais no agendamento.
    ///
    /// <para><b>É por isso que casar oferta com ocupação por código exato não funciona:</b> a escala
    /// fica no grupo e o agendamento chega com o item. Medido: casando exato sobram 7.172
    /// agendamentos sem oferta que os explique; considerando o grupo, ~3.185 deles se resolvem.</para>
    /// </summary>
    public bool EhGrupo { get; set; }

    // ---- Quando ----

    public DayOfWeek DiaSemana { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFim { get; set; }

    public DateOnly VigenciaInicio { get; set; }
    public DateOnly VigenciaFim { get; set; }

    // ---- Quantas vagas ----

    /// <summary>Vagas de primeira vez no bloco.</summary>
    public int VagasPrimeiraVez { get; set; }

    /// <summary>
    /// Minutos por vaga de primeira vez, <b>como o SISREG declara — e ele frequentemente declara
    /// zero</b>. Guardado por fidelidade, não para calcular: <c>duração == Σ(vagas × minutos)</c>
    /// só fecha em 803 dos 934 blocos vigentes. A grade de horários da tela é dedução nossa
    /// (duração ÷ vagas), exibida como estimativa.
    /// </summary>
    public int MinutosPrimeiraVez { get; set; }

    public int VagasRetorno { get; set; }
    public int MinutosRetorno { get; set; }

    public int VagasReserva { get; set; }
    public int MinutosReserva { get; set; }

    /// <summary>Soma das três categorias. Desnormalizada porque toda consulta de oferta a usa e
    /// somar três colunas em <c>WHERE</c>/<c>ORDER BY</c> impede o índice de ajudar.</summary>
    public int VagasTotal { get; set; }

    // ---- Situação ----

    /// <summary>Ver <see cref="StatusEscalaSisreg"/> — <b>Ativa ≠ vigente</b>.</summary>
    public StatusEscalaSisreg Status { get; set; }

    /// <summary>Coluna <c>AGENDA LOCAL</c> do SISREG.</summary>
    public bool AgendaLocal { get; set; }

    /// <summary>Coluna <c>QUEBRA AUTOMATICA</c> do SISREG.</summary>
    public bool QuebraAutomatica { get; set; }

    // ---- Proveniência (operadores e datas do SISREG) ----

    public string? OperadorCriador { get; set; }
    public string? OperadorModificador { get; set; }
    public DateOnly? InseridaEmSisreg { get; set; }
    public DateOnly? AlteradaEmSisreg { get; set; }
    public DateOnly? AtivadaEmSisreg { get; set; }

    // ---- Nossos ----

    /// <summary>Última vez que esta escala apareceu no arquivo.</summary>
    public DateTime VistoEm { get; set; }

    /// <summary>
    /// Deixou de vir no arquivo. Mesmo padrão de <see cref="SisregProfissionalUnidade.Ausente"/>:
    /// some da oferta sem apagar a linha, porque o histórico de "esta vaga existia" é o que permite
    /// explicar um agendamento antigo que hoje não teria escala nenhuma.
    /// </summary>
    public bool Ausente { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
