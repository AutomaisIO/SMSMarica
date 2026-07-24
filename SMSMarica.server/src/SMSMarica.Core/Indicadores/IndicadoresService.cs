using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Indicadores.Dtos;
using SMSMarica.Core.Inteligencia.Fontes;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Indicadores;

/// <summary>
/// Apura os indicadores contratuais executando o SQL cadastrado contra a base de origem.
/// Sempre read-only: o SQL passa pelo guard da <see cref="IFonteDados"/> antes de ir ao banco.
/// </summary>
public sealed class IndicadoresService(
    SmsMaricaDbContext db,
    IFonteDadosFactory fonteFactory,
    IUsuarioAtualAccessor usuarioAtual) : IIndicadoresService
{
    /// <summary>
    /// Só o Conde Modesto Leal entra na apuração — UPA Inoã (2) e PA Santa Rita (3) existem
    /// na mesma base Salux, mas estão fora do contrato desta planilha.
    /// </summary>
    private static readonly UnidadeIndicadorDto[] UnidadesContratadas =
    [
        new(1, "Hospital Municipal Conde Modesto Leal"),
    ];

    public IReadOnlyList<UnidadeIndicadorDto> Unidades() => UnidadesContratadas;

    public async Task<IReadOnlyList<IndicadorResumoDto>> ListarAsync(
        AbaIndicador aba, FiltroIndicadorDto filtro, CancellationToken ct = default)
    {
        GarantirUnidade(filtro.Hospital);

        var indicadores = await db.Indicadores
            .AsNoTracking()
            .Where(i => i.Aba == aba && i.ExcluidoEm == null)
            .OrderBy(i => i.Ordem)
            .ToListAsync(ct);

        var ids = indicadores.Select(i => i.Id).ToList();

        // Última execução gravada de cada indicador para exatamente este período/unidade.
        var execucoes = await db.IndicadorExecucoes
            .AsNoTracking()
            .Where(e => ids.Contains(e.IndicadorId)
                        && e.Hospital == filtro.Hospital
                        && e.PeriodoInicio == filtro.Inicio
                        && e.PeriodoFim == filtro.Fim)
            .GroupBy(e => e.IndicadorId)
            .Select(g => g.OrderByDescending(e => e.ExecutadoEm).First())
            .ToListAsync(ct);

        var porIndicador = execucoes.ToDictionary(e => e.IndicadorId);

        return indicadores
            .Select(i => Resumo(i, porIndicador.GetValueOrDefault(i.Id)))
            .ToList();
    }

    public async Task<IndicadorDetalheDto> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var indicador = await db.Indicadores
            .AsNoTracking()
            .Include(i => i.Fonte)
            .FirstOrDefaultAsync(i => i.Id == id && i.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Indicador", id);

        var versoes = await db.IndicadorVersoes.CountAsync(v => v.IndicadorId == id, ct);

        return Detalhe(indicador, versoes);
    }

    public async Task<IReadOnlyList<IndicadorVersaoDto>> ListarVersoesAsync(
        Guid id, CancellationToken ct = default)
    {
        var versoes = await db.IndicadorVersoes
            .AsNoTracking()
            .Where(v => v.IndicadorId == id)
            .OrderByDescending(v => v.Numero)
            .ToListAsync(ct);

        var autores = versoes.Select(v => v.CriadoPor).Where(g => g != null).Select(g => g!.Value).Distinct();
        var nomes = await db.Usuarios
            .AsNoTracking()
            .Where(u => autores.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);

        return versoes
            .Select(v => new IndicadorVersaoDto(
                v.Id, v.Numero, v.Sql, v.Nota, v.CriadoEm,
                v.CriadoPor is { } p ? nomes.GetValueOrDefault(p) : null))
            .ToList();
    }

    public async Task<IReadOnlyList<FonteIndicadorDto>> ListarFontesAsync(CancellationToken ct = default) =>
        await db.IaFontes
            .AsNoTracking()
            .Where(f => f.ExcluidoEm == null && f.Ativo)
            .OrderBy(f => f.Nome)
            .Select(f => new FonteIndicadorDto(f.Id, f.Nome))
            .ToListAsync(ct);

    public async Task<IndicadorDetalheDto> CriarAsync(SalvarIndicadorDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ValidarAsync(dto, null, ct);

        var indicador = new Indicador
        {
            Id = Guid.NewGuid(),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };

        Aplicar(indicador, dto);
        db.Indicadores.Add(indicador);

        if (!string.IsNullOrWhiteSpace(indicador.Sql))
        {
            db.IndicadorVersoes.Add(NovaVersao(indicador.Id, 1, indicador.Sql!, dto.Nota, usuarioAtual.UsuarioId));
        }

        await db.SaveChangesAsync(ct);
        return await ObterAsync(indicador.Id, ct);
    }

    public async Task<IndicadorDetalheDto> AtualizarAsync(
        Guid id, SalvarIndicadorDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var indicador = await db.Indicadores
            .Include(i => i.Fonte)
            .FirstOrDefaultAsync(i => i.Id == id && i.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Indicador", id);

        await ValidarAsync(dto, id, ct);

        var sqlNovo = string.IsNullOrWhiteSpace(dto.Sql) ? null : dto.Sql.Trim();

        // So versiona quando o SQL realmente mudou - editar meta ou peso nao polui o historico.
        if (sqlNovo is not null && sqlNovo != indicador.Sql)
        {
            var proximo = await db.IndicadorVersoes
                .Where(v => v.IndicadorId == id)
                .MaxAsync(v => (int?)v.Numero, ct) ?? 0;

            db.IndicadorVersoes.Add(NovaVersao(id, proximo + 1, sqlNovo, dto.Nota, usuarioAtual.UsuarioId));
        }

        Aplicar(indicador, dto);
        indicador.AtualizadoEm = DateTime.UtcNow;
        indicador.AtualizadoPor = usuarioAtual.UsuarioId;

        await db.SaveChangesAsync(ct);
        return await ObterAsync(id, ct);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var indicador = await db.Indicadores
            .FirstOrDefaultAsync(i => i.Id == id && i.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Indicador", id);

        if (await db.Indicadores.AnyAsync(i => i.IndicadorPaiId == id && i.ExcluidoEm == null, ct))
        {
            throw new ConflitoException(
                "indicador.agrupador",
                "Este indicador agrupa outros - remova ou reaponte os filhos antes de excluir.");
        }

        indicador.ExcluidoEm = DateTime.UtcNow;
        indicador.ExcluidoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidarAsync(SalvarIndicadorDto dto, Guid? idAtual, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
        {
            throw new ValidacaoException("indicador.nome", "Informe o nome do indicador.");
        }

        if (string.IsNullOrWhiteSpace(dto.Numero))
        {
            throw new ValidacaoException("indicador.numero", "Informe o numero do indicador na planilha.");
        }

        var duplicado = await db.Indicadores.AnyAsync(
            i => i.Aba == dto.Aba && i.Numero == dto.Numero && i.ExcluidoEm == null
                 && (idAtual == null || i.Id != idAtual), ct);

        if (duplicado)
        {
            throw new ConflitoException("indicador.numero", $"Ja existe o indicador {dto.Numero} nesta aba.");
        }

        var sql = string.IsNullOrWhiteSpace(dto.Sql) ? null : dto.Sql.Trim();

        if (sql is not null)
        {
            // Falha cedo: valida o SQL ja com os parametros resolvidos, como vai rodar de verdade.
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            var comParametros = ParametrosIndicador.Aplicar(sql, 1, hoje.AddMonths(-1), hoje);
            Inteligencia.Validacao.SqlReadOnlyGuard.GarantirLeitura(comParametros);

            if (dto.FonteId is null)
            {
                throw new ValidacaoException(
                    "indicador.fonte", "Escolha a base onde o SQL do indicador vai rodar.");
            }
        }

        if (dto.FonteId is { } fonteId &&
            !await db.IaFontes.AnyAsync(f => f.Id == fonteId && f.ExcluidoEm == null, ct))
        {
            throw new ValidacaoException("indicador.fonte", "Base de dados nao encontrada.");
        }

        if (dto.MetaOperador == MetaOperador.Entre && dto.MetaValorMaximo is null)
        {
            throw new ValidacaoException(
                "indicador.meta", "Meta em faixa precisa do valor maximo (ex.: 80 a 85%).");
        }

        if (dto.MetaOperador is not null && dto.MetaValor is null)
        {
            throw new ValidacaoException(
                "indicador.meta", "Informe o valor numerico da meta para que o indicador possa pontuar.");
        }

        if (dto.IndicadorPaiId is { } paiId)
        {
            if (paiId == idAtual)
            {
                throw new ValidacaoException("indicador.pai", "Um indicador nao pode agrupar a si mesmo.");
            }

            if (!await db.Indicadores.AnyAsync(i => i.Id == paiId && i.ExcluidoEm == null, ct))
            {
                throw new ValidacaoException("indicador.pai", "Indicador agrupador nao encontrado.");
            }
        }
    }

    private static void Aplicar(Indicador indicador, SalvarIndicadorDto dto)
    {
        indicador.Aba = dto.Aba;
        indicador.Numero = dto.Numero.Trim();
        indicador.Ordem = dto.Ordem;
        indicador.IndicadorPaiId = dto.IndicadorPaiId;
        indicador.Nome = dto.Nome.Trim();
        indicador.MemoriaCalculo = dto.MemoriaCalculo;
        indicador.FonteDeclarada = dto.FonteDeclarada;
        indicador.Meta = dto.Meta;
        indicador.MetaOperador = dto.MetaOperador;
        indicador.MetaValor = dto.MetaValor;
        indicador.MetaValorMaximo = dto.MetaValorMaximo;
        indicador.Pontuacao = dto.Pontuacao;
        indicador.TipoResultado = dto.TipoResultado;
        indicador.UnidadeMedida = dto.UnidadeMedida;
        indicador.FatorDensidade = dto.FatorDensidade;
        indicador.FonteId = dto.FonteId;
        indicador.Sql = string.IsNullOrWhiteSpace(dto.Sql) ? null : dto.Sql.Trim();
        indicador.Ressalva = dto.Ressalva;
        indicador.Ativo = dto.Ativo;

        // Sem SQL nao existe "validado": a situacao cai para SemMotor sozinha.
        indicador.Situacao = indicador.Sql is null
            && dto.Situacao is SituacaoIndicador.Validado or SituacaoIndicador.NaoValidado
                ? SituacaoIndicador.SemMotor
                : dto.Situacao;
    }

    private static IndicadorVersao NovaVersao(Guid indicadorId, int numero, string sql, string? nota, Guid? autor) =>
        new()
        {
            Id = Guid.NewGuid(),
            IndicadorId = indicadorId,
            Numero = numero,
            Sql = sql,
            Nota = nota,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = autor,
        };

    public async Task<ResultadoIndicadorDto> ApurarAsync(
        Guid id, FiltroIndicadorDto filtro, bool persistir = true, CancellationToken ct = default)
    {
        GarantirUnidade(filtro.Hospital);

        var indicador = await db.Indicadores
            .AsNoTracking()
            .Include(i => i.Fonte)
            .FirstOrDefaultAsync(i => i.Id == id && i.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Indicador", id);

        if (indicador.TipoResultado == TipoResultadoIndicador.Agrupador)
        {
            throw new ValidacaoException(
                "indicador.sql",
                "Indicador agrupador não tem cálculo próprio — o resultado é a soma dos filhos.");
        }

        if (string.IsNullOrWhiteSpace(indicador.Sql))
        {
            throw new ValidacaoException(
                "indicador.sql", "Este indicador ainda não tem motor — não há SQL para executar.");
        }

        if (indicador.Fonte is null)
        {
            throw new ValidacaoException(
                "indicador.fonte", "Este indicador não tem base de dados configurada.");
        }

        var sql = ParametrosIndicador.Aplicar(indicador.Sql, filtro.Hospital, filtro.Inicio, filtro.Fim);
        var fonte = fonteFactory.Criar(indicador.Fonte);

        var cronometro = Stopwatch.StartNew();
        var retorno = await fonte.ExecutarAsync(sql, ct);
        cronometro.Stop();

        var resultado = retorno.Sucesso
            ? Interpretar(indicador, retorno, (int)cronometro.ElapsedMilliseconds)
            : new ResultadoIndicadorDto(
                null, null, null, null, null, null,
                (int)cronometro.ElapsedMilliseconds, DateTime.UtcNow, retorno.Erro);

        if (persistir)
        {
            var versaoAtual = await db.IndicadorVersoes
                .Where(v => v.IndicadorId == id)
                .OrderByDescending(v => v.Numero)
                .Select(v => (Guid?)v.Id)
                .FirstOrDefaultAsync(ct);

            db.IndicadorExecucoes.Add(new IndicadorExecucao
            {
                Id = Guid.NewGuid(),
                IndicadorId = id,
                IndicadorVersaoId = versaoAtual,
                Hospital = filtro.Hospital,
                PeriodoInicio = filtro.Inicio,
                PeriodoFim = filtro.Fim,
                Numerador = resultado.Numerador,
                Denominador = resultado.Denominador,
                Valor = resultado.Valor,
                DistribuicaoJson = resultado.Distribuicao is { Count: > 0 }
                    ? JsonSerializer.Serialize(resultado.Distribuicao)
                    : null,
                AtingiuMeta = resultado.AtingiuMeta,
                PontuacaoApurada = resultado.PontuacaoApurada,
                DuracaoMs = resultado.DuracaoMs,
                Erro = resultado.Erro,
                ExecutadoEm = resultado.ExecutadoEm,
                ExecutadoPor = usuarioAtual.UsuarioId,
            });

            await db.SaveChangesAsync(ct);
        }

        return resultado;
    }

    public async Task<IReadOnlyList<IndicadorResumoDto>> ApurarAbaAsync(
        AbaIndicador aba, FiltroIndicadorDto filtro, CancellationToken ct = default)
    {
        GarantirUnidade(filtro.Hospital);

        var comMotor = await db.Indicadores
            .AsNoTracking()
            .Where(i => i.Aba == aba && i.ExcluidoEm == null && i.Ativo && i.Sql != null && i.FonteId != null)
            .OrderBy(i => i.Ordem)
            .Select(i => i.Id)
            .ToListAsync(ct);

        // Em sequência de propósito: o Oracle do hospital é produção viva e não é lugar
        // de abrir dezenas de sessões simultâneas para desenhar uma tela.
        foreach (var id in comMotor)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await ApurarAsync(id, filtro, persistir: true, ct);
            }
            catch (ValidacaoException)
            {
                // Motor inválido não derruba a apuração da aba inteira.
            }
        }

        return await ListarAsync(aba, filtro, ct);
    }

    private static void GarantirUnidade(int hospital)
    {
        if (Array.TrueForAll(UnidadesContratadas, u => u.Hospital != hospital))
        {
            throw new ValidacaoException(
                "indicador.unidade",
                "Unidade fora do contrato desta planilha (só o Conde Modesto Leal é apurado).");
        }
    }

    /// <summary>Lê o retorno tabular pelo nome da coluna e aplica a conta do tipo do indicador.</summary>
    private static ResultadoIndicadorDto Interpretar(
        Indicador indicador, ResultadoConsulta retorno, int duracaoMs)
    {
        var agora = DateTime.UtcNow;

        if (indicador.TipoResultado == TipoResultadoIndicador.Distribuicao)
        {
            var iRotulo = Indice(retorno, "rotulo");
            var iQtd = Indice(retorno, "quantidade");

            if (iRotulo < 0 || iQtd < 0)
            {
                return new ResultadoIndicadorDto(null, null, null, null, null, null, duracaoMs, agora,
                    "O SQL de distribuição precisa devolver as colunas 'rotulo' e 'quantidade'.");
            }

            var linhas = retorno.Linhas
                .Select(l => new LinhaDistribuicaoDto(
                    l[iRotulo]?.ToString() ?? "(sem rótulo)",
                    Decimal(l[iQtd]) ?? 0))
                .ToList();

            // Distribuição não tem meta: o valor do indicador é a própria tabela.
            return new ResultadoIndicadorDto(
                null, linhas.Sum(l => l.Quantidade), null, linhas, null, null, duracaoMs, agora, null);
        }

        if (retorno.Linhas.Count == 0)
        {
            return new ResultadoIndicadorDto(null, null, null, null, null, null, duracaoMs, agora,
                "A consulta não devolveu linha nenhuma no período.");
        }

        var linha = retorno.Linhas[0];
        var numerador = Valor(retorno, linha, "numerador");
        var denominador = Valor(retorno, linha, "denominador");
        var valorDireto = Valor(retorno, linha, "valor");

        // Espelha a planilha: resultado = numerador / denominador, com multiplicador só na
        // densidade. A exceção é Media, cujo SQL já devolve a média pronta em `valor`
        // (Σ ÷ n calculado no Oracle) — aí o numerador não se aplica.
        //
        // IMPORTANTE: numerador ausente NÃO vira zero. Tratar null como 0 mostraria "0" no
        // lugar de "faltou a coluna", que é o pior tipo de erro — número falso com cara de certo.
        string? erro = null;
        decimal? valor = null;

        switch (indicador.TipoResultado)
        {
            case TipoResultadoIndicador.Media:
                if (valorDireto is null)
                {
                    erro = "A consulta não devolveu a coluna 'valor' (média).";
                }
                valor = valorDireto;
                break;

            case TipoResultadoIndicador.Razao:
            case TipoResultadoIndicador.Densidade:
                if (numerador is null)
                {
                    erro = "A consulta não devolveu a coluna 'numerador'.";
                }
                else if (denominador is not > 0)
                {
                    erro = "Denominador zerado ou ausente no período — sem base para calcular.";
                }
                else
                {
                    valor = numerador.Value / denominador.Value;
                    if (indicador.TipoResultado == TipoResultadoIndicador.Densidade)
                    {
                        valor *= indicador.FatorDensidade ?? 1000;
                    }
                }
                break;

            case TipoResultadoIndicador.Absoluto:
                if (numerador is null)
                {
                    erro = "A consulta não devolveu a coluna 'numerador'.";
                }
                valor = numerador;
                break;
        }

        if (valor is not null)
        {
            valor = Math.Round(valor.Value, 6);
        }

        var (atingiu, pontos) = AvaliarMeta(indicador, valor);

        return new ResultadoIndicadorDto(
            valor, numerador, denominador, null, atingiu, pontos, duracaoMs, agora, erro);
    }

    /// <summary>
    /// Decide se o resultado bateu a meta e quanto o indicador pontuou no mês.
    /// Pontuação é tudo-ou-nada, como na planilha: bateu leva o peso cheio, não bateu leva zero.
    /// Sem operador de meta (ou sem resultado) devolve nulo — a tela mostra "—" em vez de
    /// fingir que o indicador foi avaliado.
    /// </summary>
    private static (bool? Atingiu, decimal? Pontos) AvaliarMeta(Indicador indicador, decimal? valor)
    {
        if (valor is null || indicador.MetaOperador is null || indicador.MetaValor is null)
        {
            return (null, null);
        }

        var alvo = indicador.MetaValor.Value;
        var atingiu = indicador.MetaOperador switch
        {
            MetaOperador.MenorOuIgual => valor.Value <= alvo,
            MetaOperador.MaiorOuIgual => valor.Value >= alvo,
            MetaOperador.Menor => valor.Value < alvo,
            MetaOperador.Maior => valor.Value > alvo,
            MetaOperador.Igual => valor.Value == alvo,
            MetaOperador.Entre => valor.Value >= alvo
                                  && valor.Value <= (indicador.MetaValorMaximo ?? alvo),
            _ => false,
        };

        return (atingiu, atingiu ? (indicador.Pontuacao ?? 0) : 0);
    }

    private static int Indice(ResultadoConsulta r, string coluna)
    {
        for (var i = 0; i < r.Colunas.Count; i++)
        {
            if (string.Equals(r.Colunas[i], coluna, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private static decimal? Valor(ResultadoConsulta r, IReadOnlyList<object?> linha, string coluna)
    {
        var i = Indice(r, coluna);
        return i < 0 ? null : Decimal(linha[i]);
    }

    private static decimal? Decimal(object? bruto) => bruto switch
    {
        null => null,
        decimal d => d,
        double db => (decimal)db,
        float f => (decimal)f,
        int n => n,
        long l => l,
        short s => s,
        string t when decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => p,
        _ => null,
    };

    private static IndicadorResumoDto Resumo(Indicador i, IndicadorExecucao? execucao) =>
        new(i.Id, i.Aba, i.Numero, i.Ordem, i.IndicadorPaiId, i.Nome, i.Meta,
            i.MetaOperador, i.MetaValor, i.MetaValorMaximo, i.Pontuacao, i.TipoResultado,
            i.UnidadeMedida, i.FatorDensidade, i.Situacao, !string.IsNullOrWhiteSpace(i.Sql), i.Ressalva,
            execucao is null
                ? null
                : new ResultadoIndicadorDto(
                    execucao.Valor, execucao.Numerador, execucao.Denominador,
                    execucao.DistribuicaoJson is null
                        ? null
                        : JsonSerializer.Deserialize<List<LinhaDistribuicaoDto>>(execucao.DistribuicaoJson),
                    execucao.AtingiuMeta, execucao.PontuacaoApurada,
                    execucao.DuracaoMs, execucao.ExecutadoEm, execucao.Erro));

    private static IndicadorDetalheDto Detalhe(Indicador i, int totalVersoes) =>
        new(i.Id, i.Aba, i.Numero, i.Ordem, i.IndicadorPaiId, i.Nome, i.MemoriaCalculo,
            i.FonteDeclarada, i.Meta, i.MetaOperador, i.MetaValor, i.MetaValorMaximo,
            i.Pontuacao, i.TipoResultado, i.UnidadeMedida, i.FatorDensidade, i.Situacao,
            i.FonteId, i.Fonte?.Nome, i.Sql, i.Ressalva, i.Ativo, totalVersoes);
}
