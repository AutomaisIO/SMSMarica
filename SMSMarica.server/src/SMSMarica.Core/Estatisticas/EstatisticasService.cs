using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Estatisticas.Dtos;
using SMSMarica.Data;

namespace SMSMarica.Core.Estatisticas;

/// <summary>
/// Agrega o histórico de mensagens WhatsApp (<c>whatsapp_mensagem</c>) e conversas em números
/// gerenciais. Usa SQL agregado direto na conexão do contexto (nunca materializa linhas) porque o
/// banco é compartilhado com outros produtos e as contagens varreriam a tabela à toa via EF.
/// </summary>
public sealed class EstatisticasService(SmsMaricaDbContext db) : IEstatisticasService
{
    private const int MaxDiasPeriodo = 400;

    // Discriminadores (ver ADR/entidades): direcao 1=Saida 2=Entrada; template preenchido = HSM;
    // autor_usuario_id nulo = envio automático do sistema, preenchido = operador na Central.
    // status: 1 Enviada, 2 Entregue, 3 Lida, 4 Falha, 5 Recebida.
    private const string FiltroPeriodoSimulado = """
        m.ocorrido_em::date BETWEEN @de AND @ate
        AND (m.conteudo IS NULL OR m.conteudo NOT LIKE '[SIMULADO]%')
        AND (m.wa_message_id IS NULL OR m.wa_message_id NOT LIKE 'simulado-%')
        """;

    public async Task<EstatisticasWhatsAppDto> ObterWhatsAppAsync(
        DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        if (ate < de) (de, ate) = (ate, de);
        if (ate.DayNumber - de.DayNumber + 1 > MaxDiasPeriodo)
            throw new ValidacaoException("periodo", $"O período não pode exceder {MaxDiasPeriodo} dias.");

        var conn = db.Database.GetDbConnection();
        var abriuAqui = conn.State != ConnectionState.Open;
        if (abriuAqui) await conn.OpenAsync(ct);
        try
        {
            var resumoBruto = await LerResumoAsync(conn, de, ate, ct);
            var porDia = await LerPorDiaAsync(conn, de, ate, ct);
            var porTemplate = await LerRotuloAsync(conn,
                $"SELECT COALESCE(NULLIF(m.template,''),'(sem template)') AS r, count(*) AS c " +
                $"FROM smsmarica.whatsapp_mensagem m WHERE {FiltroPeriodoSimulado} " +
                "AND m.template IS NOT NULL AND m.template <> '' GROUP BY 1 ORDER BY 2 DESC LIMIT 12",
                de, ate, ct);
            var porStatus = await LerStatusAsync(conn, de, ate, ct);
            var porAtendente = await LerRotuloAsync(conn,
                "SELECT COALESCE(NULLIF(m.autor_nome_exibicao,''),'(sem nome)') AS r, count(*) AS c " +
                $"FROM smsmarica.whatsapp_mensagem m WHERE {FiltroPeriodoSimulado} " +
                "AND m.autor_usuario_id IS NOT NULL GROUP BY 1 ORDER BY 2 DESC LIMIT 12",
                de, ate, ct);
            var conversasNovas = await LerEscalarAsync(conn,
                "SELECT count(*) FROM smsmarica.conversa c " +
                "WHERE c.excluido_em IS NULL AND c.criado_em::date BETWEEN @de AND @ate",
                de, ate, ct);

            var dias = ate.DayNumber - de.DayNumber + 1;
            var total = resumoBruto.Enviadas + resumoBruto.Recebidas;
            var mensagensSessao = total - resumoBruto.TemplatesSistema - resumoBruto.TemplatesAtendente;

            var resumo = new EstatisticasResumoDto(
                TotalMensagens: total,
                Enviadas: resumoBruto.Enviadas,
                Recebidas: resumoBruto.Recebidas,
                TemplatesSistema: resumoBruto.TemplatesSistema,
                TemplatesAtendente: resumoBruto.TemplatesAtendente,
                MensagensSessao: mensagensSessao,
                ConversasNovas: conversasNovas,
                DiasNoPeriodo: dias,
                MediaDiaria: dias > 0 ? Math.Round((double)total / dias, 1) : 0,
                TaxaEntrega: resumoBruto.Enviadas > 0
                    ? Math.Round(100.0 * resumoBruto.Entregues / resumoBruto.Enviadas, 1) : 0,
                TaxaLeitura: resumoBruto.Enviadas > 0
                    ? Math.Round(100.0 * resumoBruto.Lidas / resumoBruto.Enviadas, 1) : 0,
                Atendentes: resumoBruto.Atendentes);

            var porCategoria = new List<RotuloContagemDto>
            {
                new("Template (sistema)", resumoBruto.TemplatesSistema),
                new("Template (atendente)", resumoBruto.TemplatesAtendente),
                new("Atendente (sessão)", resumoBruto.TextoAtendente),
                new("Automática (sessão)", resumoBruto.TextoSistema),
                new("Recebidas", resumoBruto.Recebidas),
            };

            return new EstatisticasWhatsAppDto(de, ate, resumo, porDia, porCategoria,
                porTemplate, porStatus, porAtendente);
        }
        finally
        {
            if (abriuAqui) await conn.CloseAsync();
        }
    }

    private sealed record ResumoBruto(
        long Enviadas, long Recebidas, long TemplatesSistema, long TemplatesAtendente,
        long TextoAtendente, long TextoSistema, long Entregues, long Lidas, int Atendentes);

    private async Task<ResumoBruto> LerResumoAsync(DbConnection conn, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn,
            $"""
            SELECT
              count(*) FILTER (WHERE m.direcao=1) AS enviadas,
              count(*) FILTER (WHERE m.direcao=2) AS recebidas,
              count(*) FILTER (WHERE m.direcao=1 AND m.template IS NOT NULL AND m.template<>'' AND m.autor_usuario_id IS NULL) AS tpl_sistema,
              count(*) FILTER (WHERE m.direcao=1 AND m.template IS NOT NULL AND m.template<>'' AND m.autor_usuario_id IS NOT NULL) AS tpl_atendente,
              count(*) FILTER (WHERE m.direcao=1 AND (m.template IS NULL OR m.template='') AND m.autor_usuario_id IS NOT NULL) AS texto_atendente,
              count(*) FILTER (WHERE m.direcao=1 AND (m.template IS NULL OR m.template='') AND m.autor_usuario_id IS NULL) AS texto_sistema,
              count(*) FILTER (WHERE m.direcao=1 AND m.status IN (2,3)) AS entregues,
              count(*) FILTER (WHERE m.direcao=1 AND m.status=3) AS lidas,
              count(DISTINCT m.autor_usuario_id) FILTER (WHERE m.autor_usuario_id IS NOT NULL) AS atendentes
            FROM smsmarica.whatsapp_mensagem m
            WHERE {FiltroPeriodoSimulado}
            """, de, ate);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return new ResumoBruto(
            r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3),
            r.GetInt64(4), r.GetInt64(5), r.GetInt64(6), r.GetInt64(7), (int)r.GetInt64(8));
    }

    private async Task<IReadOnlyList<SerieDiaDto>> LerPorDiaAsync(
        DbConnection conn, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn,
            "SELECT m.ocorrido_em::date AS dia, " +
            "count(*) FILTER (WHERE m.direcao=1) AS env, count(*) FILTER (WHERE m.direcao=2) AS rec " +
            $"FROM smsmarica.whatsapp_mensagem m WHERE {FiltroPeriodoSimulado} GROUP BY 1 ORDER BY 1",
            de, ate);
        var lista = new List<SerieDiaDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new SerieDiaDto(DateOnly.FromDateTime(r.GetDateTime(0)), r.GetInt64(1), r.GetInt64(2)));
        return lista;
    }

    private async Task<IReadOnlyList<RotuloContagemDto>> LerRotuloAsync(
        DbConnection conn, string sql, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn, sql, de, ate);
        var lista = new List<RotuloContagemDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new RotuloContagemDto(r.GetString(0), r.GetInt64(1)));
        return lista;
    }

    private async Task<IReadOnlyList<RotuloContagemDto>> LerStatusAsync(
        DbConnection conn, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn,
            "SELECT m.status, count(*) FROM smsmarica.whatsapp_mensagem m " +
            $"WHERE {FiltroPeriodoSimulado} GROUP BY 1 ORDER BY 1", de, ate);
        var lista = new List<RotuloContagemDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new RotuloContagemDto(RotuloStatus(r.IsDBNull(0) ? (int?)null : r.GetInt32(0)), r.GetInt64(1)));
        return lista;
    }

    private async Task<long> LerEscalarAsync(DbConnection conn, string sql, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn, sql, de, ate);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is long l ? l : Convert.ToInt64(v);
    }

    private static DbCommand CriarComando(DbConnection conn, string sql, DateOnly de, DateOnly ate)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParam(cmd, "de", de);
        AddParam(cmd, "ate", ate);
        return cmd;
    }

    private static void AddParam(DbCommand cmd, string nome, DateOnly valor)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = nome;
        p.Value = valor;
        cmd.Parameters.Add(p);
    }

    private static string RotuloStatus(int? status) => status switch
    {
        1 => "Enviada",
        2 => "Entregue",
        3 => "Lida",
        4 => "Falha",
        5 => "Recebida",
        _ => "Outro",
    };
}
