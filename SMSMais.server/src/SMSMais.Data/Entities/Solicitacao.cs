using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Solicitação regulada (o PEDIDO) — a espinha do ecossistema de solicitações. Aderente ao FHIR
/// R4 <c>ServiceRequest</c>. Uma por marcação (SISREG) ou pedido manual, para QUALQUER natureza
/// (consulta, exame de imagem, laboratório, etc. — ver <see cref="CategoriaSolicitacao"/>).
///
/// A EXECUÇÃO/resultado NÃO vive aqui: quando existe, mora num satélite ligado por FK
/// (<see cref="ExameImagem"/> para imagem/PACS; laboratório/atendimento são satélites futuros). A
/// ausência de satélite já significa "não há execução materializada" (ex.: consulta, ECG). Ver
/// ADR-0021. Auditoria conforme ADR-0006.
/// </summary>
public class Solicitacao
{
    public Guid Id { get; set; }

    /// <summary>Aponta para fhir.patient (hub FHIR) — sem FK/navegação local.</summary>
    public Guid PacienteId { get; set; }

    /// <summary>Tipo clínico (derivado do subgrupo SIGTAP na importação). Roteia satélite/UI.</summary>
    public CategoriaSolicitacao Categoria { get; set; }

    // ---- Procedimento (como veio do SISREG; cru, sem depender de mapeamento) ----

    /// <summary>Código SIGTAP cru do procedimento (só dígitos). Ex.: "0211020036" (ECG). Null em
    /// pedidos manuais sem SIGTAP.</summary>
    public string? ProcedimentoSigtapCodigo { get; set; }

    /// <summary>Descrição do procedimento como veio na origem ("ELETROCARDIOGRAMA",
    /// "CONSULTA EM OFTALMOLOGIA").</summary>
    public string? ProcedimentoTexto { get; set; }

    /// <summary>Especialidade em TEXTO (só consulta — o SIGTAP 0301010072 colapsa todas). Ponte
    /// futura para <c>Especialidade</c> do módulo Agendamento. Null fora de consulta.</summary>
    public string? EspecialidadeTexto { get; set; }

    // ---- Unidades ----

    /// <summary>Unidade EXECUTANTE (onde será realizado).</summary>
    public Guid UnidadeExecutanteId { get; set; }
    public Unidade? UnidadeExecutante { get; set; }

    /// <summary>Unidade SOLICITANTE (que pediu) — opcional, distinta da executante.</summary>
    public Guid? UnidadeSolicitanteId { get; set; }
    public Unidade? UnidadeSolicitante { get; set; }

    // ---- Solicitante (snapshot; médico/CRM ou enfermeiro/COREN) ----

    /// <summary>FK opcional para o profissional interno cadastrado. Null quando externo/SISREG.</summary>
    public Guid? SolicitanteUsuarioId { get; set; }
    public Usuario? SolicitanteUsuario { get; set; }

    public string SolicitanteNome { get; set; } = string.Empty;
    public string SolicitanteNumConselho { get; set; } = string.Empty;
    public string SolicitanteUfConselho { get; set; } = string.Empty;

    /// <summary>CPF do solicitante (só dígitos) — uso INTERNO (âncora da importação). Não exposto.</summary>
    public string? SolicitanteCpf { get; set; }

    /// <summary>"CRM" (médico) ou "COREN" (enfermeiro). Default "CRM".</summary>
    public string SolicitanteConselho { get; set; } = "CRM";

    // ---- Regulação ----

    /// <summary>Código da solicitação (nº SISREG). Chave de idempotência da importação
    /// ([[project_sisreg_solicitacao_idempotencia]]). ≥ 9999 ou o sentinela "0000" (extra-SUS).</summary>
    public string? CodigoSolicitacao { get; set; }

    /// <summary>Chave de confirmação (recepção autoriza a execução). Mesma régua do código.</summary>
    public string? ChaveConfirmacao { get; set; }

    /// <summary>
    /// A chave de confirmação <b>como o SISREG a mostrou</b>, guardada na primeira leitura para
    /// que as seguintes (painel, app do paciente, crítica da recepção) não gastem requisição.
    /// Não é a mesma coisa que <see cref="ChaveConfirmacao"/>: aquela é o que a recepção digitou
    /// ao autorizar; esta é a referência para criticar o que foi digitado. Nunca sai no DTO da
    /// solicitação — só pelo comando de revelação, que audita quem viu.
    /// </summary>
    public string? ChaveConfirmacaoSisreg { get; set; }

    /// <summary>Instante (UTC) em que <see cref="ChaveConfirmacaoSisreg"/> foi lida no SISREG.</summary>
    public DateTime? ChaveSisregLidaEm { get; set; }

    /// <summary>
    /// Código do procedimento no SISREG (o <c>pa</c>) — coluna 1 da linha do TXT.
    ///
    /// <para><b>É opcional de fato, não por displicência:</b> medido em 10/08/2026 sobre as 3.096
    /// solicitações com RAW em produção, a coluna vem <b>vazia em 1.018 delas (33%)</b>, cobrindo 20
    /// procedimentos. Por isso ele é registrado e respeitado quando existe, mas quem identifica o
    /// procedimento é o NOME (<see cref="ProcedimentoTexto"/>), que nunca falta.</para>
    /// </summary>
    public string? ProcedimentoCodigoSisreg { get; set; }

    /// <summary>
    /// CPF do profissional que vai EXECUTAR (coluna 4 da linha do TXT) — só dígitos.
    ///
    /// <para><b>Por que existe:</b> a escala do SISREG é publicada por profissional × unidade ×
    /// procedimento. Sem carimbar aqui quem executa, dá para dizer quantas vagas a unidade ofertou,
    /// mas não "abrir a agenda do Dr. Fulano" — que é o eixo de leitura que a Agenda precisa.</para>
    ///
    /// <para><b>Nullable de fato:</b> quem entra pelo caminho pontual do <c>cons_agendas</c> não traz
    /// a coluna (o profissional era o filtro da consulta, não um campo do resultado). Medido em
    /// 04/09/2026: 21.228 das 21.235 solicitações com RAW têm o CPF; as outras 20 vieram por esse
    /// caminho. Casa com <c>sisreg_profissional_unidade.cpf</c> — na medição, 271 de 271.</para>
    /// </summary>
    public string? ProfissionalExecutanteCpf { get; set; }

    /// <summary>Nome do executante (coluna 5) — snapshot, como <see cref="SolicitanteNome"/>.
    /// Guardado junto para a tela não depender de join quando o profissional some do mapeamento.</summary>
    public string? ProfissionalExecutanteNome { get; set; }

    /// <summary>Linha CRUA do TXT do SISREG que originou esta solicitação (proveniência). Uso interno.</summary>
    public string? RawSisreg { get; set; }

    /// <summary>
    /// CID-10 informado na solicitação (coluna 35 do TXT do SISREG; o SER traz o equivalente).
    ///
    /// <para><b>Texto, não FK.</b> O CID vem da origem como código solto e pode não existir no
    /// nosso catálogo — recusar a solicitação por causa disso perderia o pedido inteiro por um
    /// campo acessório. Mesma escolha já feita em <see cref="ProcedimentoTexto"/> e em
    /// <c>SerSolicitacao.Cid</c>.</para>
    ///
    /// <para>É o que torna a análise de demanda clínica: sem ele dá para dizer quantas
    /// ultrassonografias a rede pede, não <b>por quê</b>.</para>
    /// </summary>
    public string? CidCodigo { get; set; }

    /// <summary>
    /// Natureza da vaga no SISREG (coluna 8 "Vaga (flag)" do export): Primeira Vez ou Retorno.
    /// Materializada a partir do TXT/varredura para a tela poder destacar retornos sem reparsear o
    /// <see cref="RawSisreg"/>. Null = não informado (pedido manual / origem sem o dado). Ver
    /// <see cref="TipoVaga"/>.
    /// </summary>
    public TipoVaga? TipoVaga { get; set; }

    public string? Justificativa { get; set; }
    public string? Observacoes { get; set; }

    // ---- Fluxo (regulação) ----

    public StatusSolicitacao Status { get; set; } = StatusSolicitacao.Solicitada;
    public PrioridadeSolicitacao Prioridade { get; set; } = PrioridadeSolicitacao.Eletiva;

    /// <summary>Dia em que o pedido foi feito (sem hora). Vem do TXT do SISREG.</summary>
    public DateOnly? DataSolicitacao { get; set; }

    /// <summary>Dia em que a regulação autorizou (estatística de tempos). Vem do TXT.</summary>
    public DateOnly? DataRegulacao { get; set; }

    /// <summary>Quando a marcação está agendada para acontecer (instante UTC).</summary>
    public DateTime? DataAgendada { get; set; }

    // ---- Confirmação pelo PACIENTE (WhatsApp/app) — é sobre a marcação, logo regulação ----

    public StatusConfirmacaoAgendamento StatusConfirmacao { get; set; } = StatusConfirmacaoAgendamento.Pendente;
    public DateTime? ConfirmadoEm { get; set; }

    /// <summary>Canal da resposta: "whatsapp-link" | "whatsapp-quickreply" | "app".</summary>
    public string? ConfirmadoCanal { get; set; }
    public DateTime? ConfirmacaoCanceladaEm { get; set; }
    public string? MotivoCancelamentoPaciente { get; set; }

    // ---- Recepção / autorização da execução (frente de recepção — ADR-0021) ----
    // Aplicável a qualquer categoria (todo paciente é recebido). A recepção entra com a
    // ChaveConfirmacao para AUTORIZAR; o satélite de execução (ex.: ExameImagem) reage — só então
    // o exame é enfileirado ao PACS. Ponte para um futuro Encounter.

    public DateTime? AutorizadoEm { get; set; }
    public Guid? AutorizadoPor { get; set; }

    // ---- Cancelamento (decisão da equipe) ----

    public DateTime? CanceladoEm { get; set; }
    public Guid? CanceladoPorUsuarioId { get; set; }
    public string? MotivoCancelamento { get; set; }

    // ---- Satélite de execução (0..1) ----

    /// <summary>Execução de imagem (PACS), quando a categoria é <see cref="CategoriaSolicitacao.Imagem"/>.
    /// Null enquanto não recebido/materializado e sempre para categorias sem satélite.</summary>
    public ExameImagem? ExameImagem { get; set; }

    // ---- Auditoria ADR-0006 ----

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }

    /// <summary>Proveniência: de onde a solicitação nasceu (manual / import SISREG / extensão).
    /// Null no acervo anterior ao campo. Ver <see cref="FonteSolicitacao"/>.</summary>
    public FonteSolicitacao? FonteCriacao { get; set; }

    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }
}
