using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Inteligencia.Conhecimento;
using SMSMarica.Core.Inteligencia.Dtos;
using SMSMarica.Core.Inteligencia.Fontes;
using SMSMarica.Core.Inteligencia.Provedores;
using SMSMarica.Core.Inteligencia.Validacao;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Core.Inteligencia;

/// <summary>
/// Orquestrador do módulo IA. Para cada fonte selecionada (em paralelo): cria o log da consulta,
/// recupera contexto (RAG), gera o SQL via provedor de IA, valida read-only, executa, corrige em
/// caso de erro de query (até <c>Ia:MaxTentativas</c>) gerando aprendizado auto + correção, resume
/// o resultado e devolve a resposta abstraída.
/// </summary>
public sealed class IaService(
    SmsMaricaDbContext db,
    IDbContextFactory<SmsMaricaDbContext> dbFactory,
    IProvedorIa provedor,
    IRecuperadorContexto recuperador,
    IFonteDadosFactory fonteFactory,
    IUsuarioAtualAccessor usuarioAtual,
    IConfiguration configuration) : IIaService
{
    private readonly int _maxTentativas = Math.Max(1, configuration.GetValue("Ia:MaxTentativas", 2));

    public async Task<PerguntarRespostaDto> PerguntarAsync(
        PerguntarRequest request, CancellationToken cancellationToken = default)
    {
        if (request.FonteIds.Count == 0)
        {
            throw new ValidacaoException("ia.fontes", "Selecione ao menos uma base de dados.");
        }

        var tarefas = request.FonteIds
            .Distinct()
            .Select(fonteId => ProcessarFonteAsync(fonteId, request.Pergunta, cancellationToken));

        var respostas = await Task.WhenAll(tarefas);
        return new PerguntarRespostaDto(respostas);
    }

    public async Task<IReadOnlyList<FonteResumoDto>> ListarFontesAtivasAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.IaFontes
            .AsNoTracking()
            .Where(f => f.Ativo && f.ExcluidoEm == null)
            .OrderBy(f => f.Nome)
            .Select(f => new FonteResumoDto(f.Id, f.Nome, f.Tipo.ToString(), f.Ambiente.ToString(), f.Slug ?? string.Empty))
            .ToListAsync(cancellationToken);
    }

    private async Task<RespostaIaDto> ProcessarFonteAsync(
        Guid fonteId, string pergunta, CancellationToken cancellationToken)
    {
        // DbContext próprio por fonte: o WhenAll roda em paralelo e o DbContext não é thread-safe.
        await using var ctx = await dbFactory.CreateDbContextAsync(cancellationToken);

        var fonte = await ctx.IaFontes
            .FirstOrDefaultAsync(f => f.Id == fonteId && f.Ativo && f.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException("IaFonte", fonteId);

        var cronometro = Stopwatch.StartNew();
        var consulta = new IaConsulta
        {
            Id = Guid.NewGuid(),
            FonteId = fonte.Id,
            Pergunta = pergunta,
            Status = StatusConsulta.Gerando,
            Tentativas = 0,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        ctx.IaConsultas.Add(consulta);
        await ctx.SaveChangesAsync(cancellationToken);

        try
        {
            var contexto = await recuperador.RecuperarAsync(fonte.Id, pergunta, cancellationToken);
            var dialeto = fonte.Dialeto.ToString();
            var dados = fonteFactory.Criar(fonte);

            string? sqlAnterior = null;
            string? erroAnterior = null;
            GeracaoConsultaResultado? geracao = null;
            ResultadoConsulta? resultado = null;

            for (var tentativa = 1; tentativa <= _maxTentativas; tentativa++)
            {
                consulta.Tentativas = tentativa;

                var ctxGeracao = new GeracaoConsultaContexto(
                    Pergunta: pergunta,
                    Dialeto: dialeto,
                    ConhecimentoRecuperado: contexto.ConhecimentoRecuperado,
                    AprendizadosAtivos: contexto.AprendizadosAtivos,
                    SqlAnterior: sqlAnterior,
                    ErroAnterior: erroAnterior);

                geracao = await provedor.GerarConsultaAsync(ctxGeracao, cancellationToken);
                SqlReadOnlyGuard.GarantirLeitura(geracao.Sql);

                consulta.SqlGerado = geracao.Sql;
                consulta.Visualizacao = geracao.Visualizacao;
                AcumularTokens(consulta, geracao);
                consulta.Status = StatusConsulta.Executando;

                resultado = await dados.ExecutarAsync(geracao.Sql, cancellationToken);

                if (resultado.Sucesso)
                {
                    if (tentativa > 1)
                    {
                        await PersistirCorrecaoAsync(
                            ctx, fonte.Id, consulta, sqlAnterior, geracao, erroAnterior, cancellationToken);
                    }

                    break;
                }

                // Falha de query: realimenta o provedor na próxima tentativa.
                sqlAnterior = geracao.Sql;
                erroAnterior = resultado.Erro;
            }

            cronometro.Stop();
            consulta.DuracaoMs = (int)cronometro.ElapsedMilliseconds;

            if (resultado is null || !resultado.Sucesso || geracao is null)
            {
                consulta.Status = StatusConsulta.Erro;
                consulta.Erro = resultado?.Erro ?? "Não foi possível gerar uma consulta válida.";
                await ctx.SaveChangesAsync(cancellationToken);

                return new RespostaIaDto(
                    fonte.Id, fonte.Nome, "erro",
                    Resumo: null, Visualizacao: null, Titulo: null,
                    Colunas: [], Dados: [], Sql: consulta.SqlGerado,
                    ConsultaId: consulta.Id, Erro: consulta.Erro);
            }

            var resumo = await provedor.ResumirResultadoAsync(
                pergunta, resultado.Colunas, resultado.Linhas, cancellationToken);

            consulta.Status = StatusConsulta.Sucesso;
            consulta.Erro = null;
            await ctx.SaveChangesAsync(cancellationToken);

            return new RespostaIaDto(
                fonte.Id, fonte.Nome, resultado.Linhas.Count == 0 ? "vazio" : "ok",
                Resumo: resumo,
                Visualizacao: geracao.Visualizacao,
                Titulo: geracao.Titulo,
                Colunas: resultado.Colunas,
                Dados: resultado.Linhas,
                Sql: geracao.Sql,
                ConsultaId: consulta.Id,
                Erro: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            cronometro.Stop();
            consulta.Status = StatusConsulta.Erro;
            consulta.Erro = ex.Message;
            consulta.DuracaoMs = (int)cronometro.ElapsedMilliseconds;
            await ctx.SaveChangesAsync(CancellationToken.None);

            return new RespostaIaDto(
                fonte.Id, fonte.Nome, "erro",
                Resumo: null, Visualizacao: null, Titulo: null,
                Colunas: [], Dados: [], Sql: consulta.SqlGerado,
                ConsultaId: consulta.Id, Erro: ex.Message);
        }
    }

    private async Task PersistirCorrecaoAsync(
        SmsMaricaDbContext ctx,
        Guid fonteId,
        IaConsulta consulta,
        string? sqlAntes,
        GeracaoConsultaResultado geracao,
        string? erroOriginal,
        CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var quem = usuarioAtual.UsuarioId;

        Guid? aprendizadoId = null;
        if (!string.IsNullOrWhiteSpace(geracao.InstrucaoAprendizado))
        {
            var aprendizado = new IaAprendizado
            {
                Id = Guid.NewGuid(),
                FonteId = fonteId,
                Tipo = TipoAprendizado.Correcao,
                Origem = OrigemAprendizado.Auto,
                Conteudo = geracao.InstrucaoAprendizado!,
                Ativo = true,
                CriadoEm = agora,
                CriadoPor = quem,
            };
            ctx.IaAprendizados.Add(aprendizado);
            aprendizadoId = aprendizado.Id;
        }

        ctx.IaCorrecoes.Add(new IaCorrecao
        {
            Id = Guid.NewGuid(),
            ConsultaId = consulta.Id,
            AprendizadoId = aprendizadoId,
            ErroOriginal = erroOriginal ?? string.Empty,
            SqlAntes = sqlAntes,
            SqlDepois = geracao.Sql,
            InstrucaoGerada = geracao.InstrucaoAprendizado,
            CriadoEm = agora,
            CriadoPor = quem,
        });

        await ctx.SaveChangesAsync(cancellationToken);
    }

    private static void AcumularTokens(IaConsulta consulta, GeracaoConsultaResultado geracao)
    {
        consulta.TokensEntrada = (consulta.TokensEntrada ?? 0) + geracao.TokensEntrada;
        consulta.TokensSaida = (consulta.TokensSaida ?? 0) + geracao.TokensSaida;
    }
}
