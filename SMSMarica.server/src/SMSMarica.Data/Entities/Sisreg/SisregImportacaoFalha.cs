namespace SMSMarica.Data.Entities.Sisreg;

/// <summary>Por que a linha do SISREG não virou solicitação.</summary>
public enum OrigemFalhaImportacao
{
    /// <summary>O parser rejeitou a linha (layout fora do esperado) — ela nunca chegou a ser importável.</summary>
    Parser = 1,

    /// <summary>A linha foi lida, mas o fluxo de importação falhou (sem CNS, CADSUS, unidade, etc.).</summary>
    Execucao = 2,

    /// <summary>O ARQUIVO inteiro é .txt/.csv mas não é do SISREG — nenhuma linha foi tentada.
    /// Aqui <c>LinhaRaw</c> guarda um TRECHO do conteúdo (não uma linha), e não há o que revalidar:
    /// a correção é reenviar o arquivo certo. Extensão fora de .txt/.csv nem chega aqui — é
    /// ignorada antes de qualquer leitura.</summary>
    Arquivo = 3,

    /// <summary>
    /// Veio da VARREDURA da agenda (<c>cons_agendas</c>), não de arquivo. Aqui <c>LinhaRaw</c>
    /// guarda o envelope JSON do registro observado (<c>RegistroVarreduraRaw</c>), não uma linha de
    /// TXT — é o discriminador que faz o "Validar" e o modal de detalhe lerem o RAW pela lente certa.
    /// </summary>
    Varredura = 4,
}

/// <summary>
/// A causa da falha, tipada — o que decide QUAL AÇÃO a tela oferece para cada linha.
///
/// Distinta de <see cref="SisregImportacaoFalha.Motivo"/> de propósito: <c>Motivo</c> é o texto que
/// o operador lê, <c>Causa</c> é o que o sistema decide. Derivar a segunda do primeiro em tempo de
/// leitura funcionaria hoje e quebraria — em silêncio — na primeira vez que alguém melhorasse uma
/// mensagem de erro. Por isso é carimbada no ponto de aborto, onde a informação é inequívoca.
///
/// Note o que NÃO está aqui: SIGTAP sem tipo mapeado <b>não é falha</b>. A importação segue e cria a
/// solicitação pendente de mapeamento (ADR-0021), tratada na tela de Mapeamento SIGTAP. Ver ADR-0035.
/// Não confundir com <see cref="SigtapNaoMapeado"/>, que é o caso oposto e só existe na varredura:
/// lá o código SIGTAP <b>não existe</b>, e sem ele a solicitação nem categoria consegue ter.
/// </summary>
public enum CausaFalhaImportacao : short
{
    /// <summary>A marcação veio sem CNS do paciente. Sem ação automática — o dado não existe na
    /// origem; só resta descartar com nota.</summary>
    SemCns = 1,

    /// <summary>O CADSUS/cadweb50 falhou ou está indisponível (o limite de 500 req/h estoura em lote
    /// grande). Transitório: a ação certa é simplesmente revalidar mais tarde.</summary>
    CadsusIndisponivel = 2,

    /// <summary>O CADSUS não devolveu CPF para este CNS e o paciente ainda não existe no sistema.
    /// <b>É a causa acionável</b>: o operador informa o CPF e a linha é reimportada.</summary>
    CpfNaoResolvido = 3,

    /// <summary>Não foi possível identificar a unidade executante. A ação é entrar no contexto da
    /// unidade certa e revalidar.</summary>
    UnidadeNaoResolvida = 4,

    /// <summary>O parser rejeitou a linha (layout fora do esperado). Corrige-se na origem.</summary>
    LinhaInvalida = 5,

    /// <summary>O arquivo inteiro não é do SISREG. A correção é reenviar o arquivo certo.</summary>
    ArquivoIncompativel = 6,

    /// <summary>
    /// <b>Só da varredura.</b> O procedimento do SISREG (o <c>pa</c>) ainda não tem código SIGTAP
    /// confirmado no de-para, e a agenda não informa SIGTAP nenhum. Sem ele a solicitação nasceria
    /// com categoria <c>Outro</c>, sem worklist — então a linha vira pendência em vez de virar lixo.
    /// <b>É acionável</b>: o operador confirma o SIGTAP daquele procedimento e revalida; o replay
    /// resolve o de-para de novo e a linha entra. Resolve todas as pendências do mesmo <c>pa</c>.
    /// </summary>
    SigtapNaoMapeado = 7,

    /// <summary>Falha não classificada — inclusive o acervo anterior ao backfill.</summary>
    Outro = 99,
}

/// <summary>
/// Uma linha do export do SISREG que NÃO virou solicitação — gravada na hora em que falha, com o
/// conteúdo RAW da linha. É o insumo do reprocessamento direcionado: o operador corrige a causa
/// (cadastra o paciente, entra no contexto da unidade, mapeia o SIGTAP…) e clica em "Validar" —
/// aí a linha é reimportada a partir do <see cref="LinhaRaw"/>, sem precisar do arquivo de novo.
/// Como a importação é idempotente por nº do SISREG, revalidar uma linha já criada resolve a falha
/// em vez de duplicar (ver <c>ImportacaoSisregService</c>).
/// </summary>
public class SisregImportacaoFalha
{
    public Guid Id { get; set; }

    /// <summary>Nº da solicitação no SISREG — chave natural do reprocessamento. NULL quando a
    /// linha é tão malformada que nem o código deu para ler (falha de <see cref="OrigemFalhaImportacao.Parser"/>).</summary>
    public string? CodigoSolicitacao { get; set; }

    /// <summary>SHA-256 (hex) do <see cref="LinhaRaw"/> — dedupe das linhas sem código.</summary>
    public string HashLinha { get; set; } = string.Empty;

    /// <summary>Conteúdo RAW da linha, as-is. É o que o "Validar" reprocessa.</summary>
    public string LinhaRaw { get; set; } = string.Empty;

    public OrigemFalhaImportacao Origem { get; set; }

    /// <summary>Causa tipada — decide a ação que a tela oferece. Ver <see cref="CausaFalhaImportacao"/>.</summary>
    public CausaFalhaImportacao Causa { get; set; } = CausaFalhaImportacao.Outro;

    /// <summary>Motivo da falha, em linguagem de operador.</summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Nome do arquivo de onde a linha veio (proveniência; o CSV deriva o executante dele).</summary>
    public string? NomeArquivo { get; set; }

    /// <summary>Importação que gerou esta falha — dá pra abrir a execução e ver os erros dela.
    /// NULL nas falhas geradas fora de um lote (ex.: preview avulso).</summary>
    public Guid? ExecucaoId { get; set; }

    /// <summary>CNES da unidade executante do cabeçalho do arquivo — recompõe o contexto no reprocesso.</summary>
    public string? CnesExecutante { get; set; }

    /// <summary>Nome da unidade executante do cabeçalho/arquivo — idem.</summary>
    public string? NomeExecutante { get; set; }

    /// <summary>Paciente/procedimento carimbados para a lista não precisar reparsear o RAW.</summary>
    public string? NomePaciente { get; set; }
    public string? ProcedimentoTexto { get; set; }
    public DateTime? DataAgendada { get; set; }

    /// <summary>CNS do paciente, carimbado do RAW no momento da falha. É a chave mais confiável de
    /// busca (a recepção procura pela pessoa que "não tem agendamento") e o que pré-carrega a
    /// resolução por CPF. NULL no acervo anterior — o backfill não reparseia o RAW histórico.</summary>
    public string? PacienteCns { get; set; }

    /// <summary>Quantas vezes já se tentou importar esta linha (o "Validar" incrementa).</summary>
    public int Tentativas { get; set; }

    public Guid? UnidadeExecutanteId { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    /// <summary>Setado quando a linha finalmente virou solicitação (ou já existia), ou quando o
    /// operador descartou. NULL = pendente. Sai da lista de pendências.</summary>
    public DateTime? ResolvidoEm { get; set; }

    public Guid? ResolvidoPor { get; set; }

    /// <summary>Como foi resolvida (importada, já existia, descartada) — para a auditoria da lista.</summary>
    public string? ResolucaoNota { get; set; }

    /// <summary>Solicitação que passou a existir para este código (quando resolvida).</summary>
    public Guid? SolicitacaoId { get; set; }
}
