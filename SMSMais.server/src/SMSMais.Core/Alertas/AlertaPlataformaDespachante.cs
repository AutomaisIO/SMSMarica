using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data;
using SMSMais.Data.Entities.Alertas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Alertas;

/// <summary>
/// Faz o trabalho de um aviso: conta a ocorrência na fonte, aplica silêncio/freio/teto, manda para
/// cada telefone e registra o desfecho no histórico. Scoped (um por evento, DbContext fresco).
/// </summary>
public sealed partial class AlertaPlataformaDespachante(
    SmsMaisDbContext db,
    IWhatsAppCliente whatsApp,
    IAlertaDestinatarios destinatarios,
    IOptions<AlertaPlataformaOptions> opcoes,
    ILogger<AlertaPlataformaDespachante> logger)
{
    /// <summary>Sem ocorrência por este tempo, a fonte "esfria" e o próximo erro avisa na hora.</summary>
    private static readonly TimeSpan Esfriamento = TimeSpan.FromHours(12);

    /// <summary>Janela de atendimento da Meta com folga: texto livre só dentro dela.</summary>
    private static readonly TimeSpan JanelaTextoLivre = TimeSpan.FromHours(23);

    /// <summary>
    /// Intervalo mínimo até o próximo aviso da MESMA fonte, crescendo enquanto ela continua
    /// falhando: 30 min, 1 h, 2 h, 4 h, 8 h, 16 h. Um erro que se repete a cada tick de 20 s não
    /// pode virar 4.000 mensagens por dia — e um alerta que chega toda hora deixa de ser lido.
    /// </summary>
    public static TimeSpan IntervaloApos(int avisosSeguidos) =>
        TimeSpan.FromMinutes(30 * Math.Pow(2, Math.Clamp(avisosSeguidos - 1, 0, 5)));

    /// <returns>O envio registrado, ou null quando o evento só foi contado (silêncio/freio).</returns>
    public async Task<AlertaEnvio?> ProcessarAsync(EventoAlerta evento, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var origem = await ContarOcorrenciaAsync(evento, agora, ct);

        if (!evento.Forcar)
        {
            if (origem.Silenciada)
            {
                await db.SaveChangesAsync(ct);
                return null;
            }

            if (origem.UltimoAvisoEm is { } ultimo && agora - ultimo < IntervaloApos(origem.AvisosSeguidos))
            {
                origem.OcorrenciasSemAviso++;
                await db.SaveChangesAsync(ct);
                return null;
            }
        }

        var envio = new AlertaEnvio
        {
            Id = Guid.CreateVersion7(),
            OrigemChave = origem.Chave,
            Titulo = Limitar(evento.Titulo, 300),
            Detalhe = evento.Detalhe,
            CriadoEm = agora,
            Ocorrencias = 1 + origem.OcorrenciasSemAviso,
        };
        db.AlertaEnvios.Add(envio);

        var telefones = await destinatarios.ListarAtivosAsync(ct);
        if (telefones.Count == 0)
        {
            envio.Situacao = SituacaoAlertaEnvio.SemDestinatario;
            envio.Resultado = "Nenhum telefone cadastrado para receber avisos.";
        }
        else if (!evento.Forcar && await EnviadosHojeAsync(ct) >= opcoes.Value.TetoDiario)
        {
            envio.Situacao = SituacaoAlertaEnvio.TetoDiario;
            envio.Resultado = $"Teto de {opcoes.Value.TetoDiario} avisos/dia atingido — não enviado.";
        }
        else
        {
            var linhas = new List<string>(telefones.Count);
            var ok = 0;
            foreach (var telefone in telefones)
            {
                var (sucesso, linha) = await EnviarAsync(telefone, origem, envio, ct);
                if (sucesso) ok++;
                linhas.Add(linha);
            }

            envio.Resultado = string.Join('\n', linhas);
            envio.Situacao = ok == telefones.Count ? SituacaoAlertaEnvio.Enviado
                : ok == 0 ? SituacaoAlertaEnvio.Falhou
                : SituacaoAlertaEnvio.Parcial;
        }

        // O freio anda mesmo quando o envio falhou: tentar de novo a cada ocorrência não conserta
        // template recusado nem número errado — só multiplica o erro no histórico.
        origem.UltimoAvisoEm = agora;
        origem.AvisosSeguidos++;
        origem.OcorrenciasSemAviso = 0;

        await db.SaveChangesAsync(ct);
        return envio;
    }

    private async Task<AlertaOrigem> ContarOcorrenciaAsync(EventoAlerta evento, DateTime agora, CancellationToken ct)
    {
        var origem = await db.AlertaOrigens.FirstOrDefaultAsync(o => o.Chave == evento.Chave, ct);
        if (origem is null)
        {
            var conhecida = AlertaCatalogo.Buscar(evento.Chave);
            origem = new AlertaOrigem
            {
                Chave = Limitar(evento.Chave, 200),
                Rotulo = Limitar(evento.Rotulo ?? conhecida?.Rotulo ?? evento.Chave, 200),
                Grupo = Limitar(evento.Grupo ?? conhecida?.Grupo ?? "Sistema", 60),
                CriadaEm = agora,
            };
            db.AlertaOrigens.Add(origem);
        }

        if (origem.UltimaOcorrenciaEm is { } anterior && agora - anterior > Esfriamento)
            origem.AvisosSeguidos = 0;

        origem.Ocorrencias++;
        origem.UltimaOcorrenciaEm = agora;
        origem.UltimoTitulo = Limitar(evento.Titulo, 300);
        origem.UltimoDetalhe = evento.Detalhe;
        return origem;
    }

    private Task<int> EnviadosHojeAsync(CancellationToken ct)
    {
        var inicio = FusoBrasilia.InicioDoDiaAtualEmUtc();
        return db.AlertaEnvios.CountAsync(
            e => e.CriadoEm >= inicio
                && (e.Situacao == SituacaoAlertaEnvio.Enviado || e.Situacao == SituacaoAlertaEnvio.Parcial),
            ct);
    }

    /// <summary>
    /// Janela aberta (a pessoa escreveu para o número nas últimas 23 h) → texto livre, que é de
    /// graça e leva o detalhe inteiro. Fechada → template <c>erro_plataforma</c>.
    ///
    /// <para><b>Por que não "tenta o texto e, se falhar, manda o template"</b> (como fazia o aviso
    /// de sincronismo): fora da janela a Meta ACEITA o texto e só avisa que não entregou depois,
    /// pelo webhook. O envio "dá certo" e nada chega — é o "nunca recebi nada". Decidir pela
    /// janela antes de mandar é o único jeito de não depender dessa resposta tardia.</para>
    /// </summary>
    private async Task<(bool Ok, string Linha)> EnviarAsync(
        string telefone, AlertaOrigem origem, AlertaEnvio envio, CancellationToken ct)
    {
        try
        {
            if (await JanelaAbertaAsync(telefone, ct))
            {
                var texto = await whatsApp.EnviarTextoAsync(telefone, MontarTexto(origem, envio), ct: ct);
                if (texto.Ok) return (true, $"{telefone}: texto enviado");
                // Janela aberta e mesmo assim falhou: o template ainda pode passar.
                logger.LogInformation("ALERTA: texto recusado para {Telefone} ({Erro}); tentando template.", telefone, texto.Erro);
            }

            var op = opcoes.Value;
            var parametros = await MontarParametrosAsync(origem, envio, ct);
            var legivel = $"[{op.Template}] {string.Join(" | ", parametros)}";
            var r = await whatsApp.EnviarTemplateAsync(
                telefone, op.Template, op.Idioma, parametros, conteudoLegivel: legivel, ct: ct);
            return r.Ok
                ? (true, $"{telefone}: template {op.Template} enviado")
                : (false, $"{telefone}: template {op.Template} recusado — {r.Erro}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "ALERTA: falha ao avisar {Telefone}.", telefone);
            return (false, $"{telefone}: erro ao enviar — {ex.Message}");
        }
    }

    private Task<bool> JanelaAbertaAsync(string telefone, CancellationToken ct)
    {
        var desde = DateTime.UtcNow - JanelaTextoLivre;
        var sufixo = UltimosDez(telefone);
        return db.MensagensWhatsApp.AsNoTracking().AnyAsync(
            m => m.Direcao == DirecaoMensagem.Entrada && m.OcorridoEm >= desde && m.Telefone.EndsWith(sufixo),
            ct);
    }

    private static string MontarTexto(AlertaOrigem origem, AlertaEnvio envio)
    {
        var sb = new StringBuilder();
        sb.Append("*⚠ ").Append(origem.Rotulo).Append(" — ").Append(envio.Titulo).Append("*\n\n");
        sb.Append(Limitar(envio.Detalhe, 3000));
        sb.Append("\n\n").Append(Quando(envio.CriadoEm));
        if (envio.Ocorrencias > 1) sb.Append($" · {envio.Ocorrencias} ocorrências desde o último aviso");
        return sb.ToString();
    }

    /// <summary>
    /// Variáveis do template na ordem configurada, ajustadas ao número de variáveis do template
    /// APROVADO (lido do catálogo da Meta). Enquanto ele não aparece aprovado, vale a configuração.
    /// </summary>
    private async Task<List<string>> MontarParametrosAsync(AlertaOrigem origem, AlertaEnvio envio, CancellationToken ct)
    {
        var op = opcoes.Value;
        var aprovado = (await whatsApp.ListarTemplatesAsync(ct))
            .FirstOrDefault(t => string.Equals(t.Nome, op.Template, StringComparison.OrdinalIgnoreCase));
        var quantos = aprovado?.Parametros is > 0 ? aprovado.Parametros : op.Parametros.Length;

        var lista = new List<string>(quantos);
        for (var i = 0; i < quantos; i++)
        {
            var campo = i < op.Parametros.Length ? op.Parametros[i] : null;
            lista.Add(ParaVariavel(ValorDoCampo(campo, origem, envio)));
        }
        return lista;
    }

    private static string ValorDoCampo(string? campo, AlertaOrigem origem, AlertaEnvio envio) =>
        campo?.Trim().ToLowerInvariant() switch
        {
            "resumo" => envio.Ocorrencias > 1
                ? $"{origem.Rotulo} — {envio.Titulo}: {envio.Detalhe} ({envio.Ocorrencias} ocorrências)"
                : $"{origem.Rotulo} — {envio.Titulo}: {envio.Detalhe}",
            "origem" => origem.Rotulo,
            "titulo" => envio.Titulo,
            "detalhe" => envio.Detalhe,
            "descricao" => envio.Ocorrencias > 1
                ? $"{envio.Titulo}: {envio.Detalhe} ({envio.Ocorrencias} ocorrências)"
                : $"{envio.Titulo}: {envio.Detalhe}",
            "quando" => Quando(envio.CriadoEm),
            "ocorrencias" => envio.Ocorrencias.ToString(),
            _ => "-",
        };

    /// <summary>
    /// A Meta recusa variável com quebra de linha, tabulação ou mais de 4 espaços seguidos, e
    /// variável vazia. Tudo vira uma linha só, com teto de tamanho. Asterisco sai: no
    /// <c>erro_plataforma</c> a variável fica DENTRO de <c>*…*</c>, e um asterisco do detalhe
    /// fecharia o negrito no meio.
    /// </summary>
    public static string ParaVariavel(string? valor)
    {
        var linha = EspacosRegex().Replace((valor ?? string.Empty).Replace("*", string.Empty), " ").Trim();
        if (linha.Length == 0) return "-";
        return linha.Length <= 700 ? linha : linha[..697] + "...";
    }

    private static string Quando(DateTime utc) => FusoBrasilia.ParaExibicao(utc).ToString("dd/MM HH:mm");

    private static string UltimosDez(string telefone) => telefone.Length > 10 ? telefone[^10..] : telefone;

    private static string Limitar(string? v, int max) =>
        string.IsNullOrEmpty(v) ? string.Empty : v.Length <= max ? v : v[..max];

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRegex();
}
