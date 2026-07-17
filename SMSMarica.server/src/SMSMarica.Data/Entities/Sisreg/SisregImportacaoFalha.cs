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
