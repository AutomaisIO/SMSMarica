using Serilog.Core;
using Serilog.Events;
using SMSMais.Core.Alertas;

namespace SMSMais.Api.Alertas;

/// <summary>
/// Todo <c>LogError</c>/<c>LogCritical</c> do código da plataforma vira aviso no celular. É a rede
/// de segurança do "qualquer erro que eu for acrescentando": ninguém precisa lembrar de chamar o
/// aviso num motor novo — basta ele logar o erro como erro.
///
/// <para>Só enfileira (<see cref="IAlertaPlataforma.Reportar"/> não bloqueia): um sink do Serilog
/// roda na thread de quem loga, e o log não pode ficar esperando o WhatsApp.</para>
/// </summary>
public sealed class AlertaSerilogSink(IAlertaPlataforma alerta) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Error) return;
        if (!logEvent.Properties.TryGetValue("SourceContext", out var ctx)
            || ctx is not ScalarValue { Value: string categoria })
            return;
        if (!AlertaCatalogo.CapturarDoLog(categoria)) return;

        // Desligamento do servidor: vários motores gravam o cancelamento do deploy como "Erro".
        // Não é falha — e avisar a cada deploy ensina a ignorar o aviso.
        if (EhCancelamento(logEvent.Exception)) return;

        var modelo = logEvent.MessageTemplate.Text;
        var (chave, rotulo, grupo) = AlertaCatalogo.OrigemDoLog(categoria, modelo);
        var mensagem = logEvent.RenderMessage();

        var detalhe = logEvent.Exception is { } ex
            ? $"{mensagem}\n\n{ex.GetType().Name}: {Primeira(ex.Message)}"
              + (ex.InnerException is { } inner ? $"\n{inner.GetType().Name}: {Primeira(inner.Message)}" : string.Empty)
            : mensagem;

        alerta.Reportar(new EventoAlerta(chave, Primeira(mensagem, 200), detalhe)
        {
            Rotulo = rotulo,
            Grupo = grupo,
            OcorridoEm = logEvent.Timestamp.UtcDateTime,
        });
    }

    private static bool EhCancelamento(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is OperationCanceledException) return true;
        return false;
    }

    private static string Primeira(string texto, int max = 500)
    {
        var linha = texto.Split('\n', 2)[0].Trim();
        return linha.Length <= max ? linha : linha[..max];
    }
}
