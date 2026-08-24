namespace SMSMais.Core.Pacs;

/// <summary>
/// Cache em disco (LRU) para conteúdo PACS IMUTÁVEL por instância — apenas
/// caminhos sob <c>/instances/</c> (frames, bulk da instância e metadata da
/// instância). O pixel data de uma SOP Instance DICOM nunca muda, então é seguro
/// guardá-lo localmente e servir do disco em vez de bater no dcm4chee a cada
/// visualização. NUNCA cachear listagens/buscas (QIDO) nem metadata AGREGADA de
/// estudo/série (<c>studies/{u}/metadata</c>, <c>.../series/{u}/metadata</c>),
/// que mudam quando novas imagens chegam.
/// </summary>
public interface IPacsCache
{
    /// <summary>Liga/desliga o cache (config <c>Pacs:Cache:Habilitado</c>).</summary>
    bool Habilitado { get; }

    /// <summary>
    /// Teto de bytes por item: respostas maiores que isto não são cacheadas
    /// (seguem por streaming). Config <c>Pacs:Cache:TamanhoMaximoItemMb</c>.
    /// </summary>
    long TetoItemBytes { get; }

    /// <summary>
    /// Chave estável do cache = SHA256 hex de <c>metodo|caminho|queryString</c>,
    /// mais um discriminante da representação (transfer-syntax configurado) — assim
    /// ligar/desligar <c>Pacs:Dcm4chee:TransferSyntaxPreferido</c> não faz o cache
    /// servir bytes na sintaxe antiga. Usada tanto pelo proxy (leitura) quanto pelo
    /// pré-aquecimento (escrita): ambos passam pela mesma config, então as chaves batem.
    /// </summary>
    string CalcularChave(string metodo, string caminho, string queryString);

    /// <summary>
    /// Existe item para a chave? Checagem leve (só <c>File.Exists</c>), sem ler os
    /// bytes — usada pelo pré-aquecimento para pular o que já está em cache.
    /// </summary>
    bool Contains(string chave);

    /// <summary>Tenta servir do disco. Atualiza o "último acesso" no hit (LRU).</summary>
    bool TryGet(string chave, out string contentType, out byte[] bytes);

    /// <summary>Grava no disco e aplica o teto total por evicção LRU.</summary>
    void Set(string chave, string contentType, byte[] bytes);

    /// <summary>
    /// Remove um item do cache (os dois arquivos <c>{chave}.bin</c> + <c>{chave}.ct</c>).
    /// Best-effort e idempotente: se a chave não existe, não faz nada. Usado para
    /// descartar uma entrada corrompida e forçar a regeneração no próximo GET.
    /// </summary>
    void Invalidar(string chave);
}
