using System.Threading.Channels;

namespace SMSMais.Core.Alertas;

/// <summary>Um erro a reportar ao celular.</summary>
/// <param name="Chave">Fonte (ver <see cref="AlertaCatalogo"/>). O freio de repetição é por chave.</param>
public sealed record EventoAlerta(string Chave, string Titulo, string Detalhe)
{
    /// <summary>Nome legível da fonte — só usado na primeira vez, para criar a linha.</summary>
    public string? Rotulo { get; init; }

    public string? Grupo { get; init; }

    /// <summary>Telefones que recebem além dos da plataforma (os da integração, no sincronismo).</summary>
    public IReadOnlyList<string> TelefonesExtras { get; init; } = [];

    /// <summary>Ignora freio, silêncio e teto diário. Só para o botão de teste.</summary>
    public bool Forcar { get; init; }

    public DateTime OcorridoEm { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Porta de entrada dos avisos de erro da plataforma. <b>Nunca lança e nunca espera</b>: o evento
/// entra numa fila em memória e o <see cref="AlertaPlataformaWorker"/> manda depois, num escopo
/// próprio. Quem reporta costuma estar no meio de uma falha — com o DbContext quebrado, a conexão
/// caída ou a requisição cancelada — e não pode ser atrasado nem derrubado pelo aviso.
/// </summary>
public interface IAlertaPlataforma
{
    void Reportar(EventoAlerta evento);
}

public static class AlertaPlataformaExtensions
{
    public static void Reportar(this IAlertaPlataforma alerta, string chave, string titulo, string detalhe) =>
        alerta.Reportar(new EventoAlerta(chave, titulo, detalhe));
}

/// <summary>
/// Fila em memória (singleton). Limitada e com descarte: numa tempestade de erros, perder o
/// milésimo aviso igual é melhor que travar o log de quem está gravando o erro.
/// </summary>
public sealed class AlertaPlataformaFila : IAlertaPlataforma
{
    private readonly Channel<EventoAlerta> _canal = Channel.CreateBounded<EventoAlerta>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public ChannelReader<EventoAlerta> Leitor => _canal.Reader;

    public void Reportar(EventoAlerta evento) => _canal.Writer.TryWrite(evento);
}
