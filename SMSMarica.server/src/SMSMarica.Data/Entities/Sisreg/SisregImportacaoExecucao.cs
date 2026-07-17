namespace SMSMarica.Data.Entities.Sisreg;

public enum StatusImportacaoArquivo
{
    Pendente = 1,
    EmExecucao = 2,
    Concluida = 3,

    /// <summary>É .txt/.csv, mas o conteúdo não é do export de agendamentos do SISREG.
    /// O arquivo inteiro é descartado (nenhuma linha é tentada) e vira uma falha na aba Erros.</summary>
    ArquivoIncompativel = 4,

    /// <summary>Erro inesperado processando o arquivo (não é "arquivo incompatível").</summary>
    Erro = 5,

    Cancelada = 6,
}

/// <summary>
/// Uma importação de UM arquivo — a linha da aba de rastreio: quando, quem, qual arquivo, quantos
/// entraram e quantos deram erro. Arquivos de um mesmo envio compartilham o <see cref="LoteId"/>.
/// <para>
/// Só existe a partir do momento em que o SERVIDOR passou a processar o lote. Antes, o navegador
/// chamava /executar uma vez por registro e não havia o conceito de "uma importação" — por isso
/// contador nenhum era confiável (fechar a aba deixava tudo pela metade).
/// </para>
/// <para>
/// Arquivo com extensão fora de .txt/.csv NÃO gera linha aqui: é ignorado antes de qualquer
/// leitura, e só sinalizado na resposta do envio.
/// </para>
/// </summary>
public class SisregImportacaoExecucao
{
    public Guid Id { get; set; }

    /// <summary>Agrupa os arquivos enviados juntos (um upload / um zip).</summary>
    public Guid LoteId { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    /// <summary>Caminho dentro do zip, quando veio de um (proveniência).</summary>
    public string? CaminhoNoZip { get; set; }

    public StatusImportacaoArquivo Status { get; set; }

    /// <summary>Linhas de dados que o parser leu (o denominador do progresso).</summary>
    public int TotalRegistros { get; set; }

    /// <summary>Viraram solicitação agora, ou já existiam (idempotência) — o arquivo está honrado.</summary>
    public int Validos { get; set; }

    /// <summary>Não entraram e viraram falha na aba Erros.</summary>
    public int Invalidos { get; set; }

    /// <summary>Já existiam antes (subconjunto de <see cref="Validos"/>) — separa "importei" de "já tinha".</summary>
    public int JaExistiam { get; set; }

    /// <summary>Por que o arquivo foi recusado inteiro (status ArquivoIncompativel) ou quebrou.</summary>
    public string? Mensagem { get; set; }

    public DateTime IniciadoEm { get; set; }
    public DateTime? ConcluidoEm { get; set; }

    /// <summary>Quem disparou. Capturado NA REQUEST — dentro do runner não há usuário logado.</summary>
    public Guid? CriadoPor { get; set; }

    /// <summary>Nome desnormalizado: o rastreio tem que sobreviver à edição/exclusão do usuário.</summary>
    public string? CriadoPorNome { get; set; }

    /// <summary>Unidade executante ativa no momento do envio (tenant) — filtra a listagem.</summary>
    public Guid? UnidadeExecutanteId { get; set; }
}
