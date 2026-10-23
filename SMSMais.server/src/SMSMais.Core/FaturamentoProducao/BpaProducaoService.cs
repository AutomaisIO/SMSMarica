using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.FaturamentoProducao.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.FaturamentoProducao;

namespace SMSMais.Core.FaturamentoProducao;

public sealed class BpaProducaoService(
    SmsMaisDbContext db,
    IPacienteResolver pacienteResolver,
    IUsuarioAtualAccessor usuarioAtual) : IBpaProducaoService
{
    // Brasil não tem mais horário de verão; ainda assim resolvemos pelo fuso oficial (IANA, ok no
    // .NET 10 em Windows e Linux) para que a "competência" (mês do atendimento) use o dia local.
    private static readonly TimeZoneInfo Brasilia = ResolverFuso();

    public async Task<IReadOnlyList<UnidadeHabilitadaDto>> ListarUnidadesHabilitadasAsync(CancellationToken ct = default)
    {
        var (unidades, habil) = await UnidadesDoEscopoComFlagAsync(ct);
        return [.. unidades.Where(u => habil.GetValueOrDefault(u.Id))
            .Select(u => new UnidadeHabilitadaDto(u.Id, u.Nome, u.Cnes))];
    }

    public async Task<IReadOnlyList<ConfigUnidadeDto>> ListarConfiguracaoAsync(CancellationToken ct = default)
    {
        var (unidades, habil) = await UnidadesDoEscopoComFlagAsync(ct);
        return [.. unidades.Select(u => new ConfigUnidadeDto(u.Id, u.Nome, u.Cnes, habil.GetValueOrDefault(u.Id)))];
    }

    public async Task DefinirHabilitadaAsync(Guid unidadeId, bool habilitada, CancellationToken ct = default)
    {
        if (!await db.Unidades.AnyAsync(u => u.Id == unidadeId, ct))
            throw new NaoEncontradoException(nameof(Unidade), unidadeId);

        var row = await db.FaturamentoProducaoUnidades.FirstOrDefaultAsync(x => x.UnidadeId == unidadeId, ct);
        if (row is null)
        {
            row = new FaturamentoProducaoUnidade { UnidadeId = unidadeId };
            db.FaturamentoProducaoUnidades.Add(row);
        }
        row.Habilitada = habilitada;
        row.AtualizadoEm = DateTime.UtcNow;
        row.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<BpaPreviewDto> PreviewAsync(Guid unidadeId, int competencia, CancellationToken ct = default)
    {
        var (unidade, itens) = await MontarAsync(unidadeId, competencia, ct);

        // folha/sequência das linhas válidas (mesma paginação do arquivo)
        var fontes = itens.Where(i => i.Fonte is not null).Select(i => i.Fonte!).ToList();
        var pag = BpaArquivo.Paginar(fontes);
        var posicao = pag.ToDictionary(p => p.Fonte.SolicitacaoId, p => (p.Folha, p.Sequencia));
        var folhas = pag.Count == 0 ? 0 : pag[^1].Folha;
        var controle = BpaArquivo.Controle(
            fontes.Sum(f => long.TryParse(new string(f.ProcedimentoSigtap.Where(char.IsDigit).ToArray()), out var pa) ? pa : 0),
            fontes.Sum(f => (long)(f.Quantidade <= 0 ? 1 : f.Quantidade)));

        var linhas = itens.Select(i =>
        {
            int? folha = null, seq = null;
            if (i.Fonte is not null && posicao.TryGetValue(i.Sol.Id, out var pos)) { folha = pos.Folha; seq = pos.Sequencia; }
            return new BpaLinhaDto(
                folha, seq,
                i.Sol.ProcedimentoSigtapCodigo, i.Sol.ProcedimentoTexto, i.Sol.CidCodigo,
                i.CnsProfissional, i.Cbo, i.Sol.ProfissionalExecutanteNome ?? i.NomeProfissional,
                i.Pac?.Nome ?? string.Empty, i.Pac?.Cns, i.Pac?.Cpf, i.Pac?.DataNascimento,
                SexoStr(i.Pac?.Sexo), i.DataAtendimento, i.Fonte is not null, i.Motivos);
        }).ToList();

        var est = new BpaEstatisticasDto(
            Total: itens.Count,
            Validas: fontes.Count,
            Pendencias: itens.Count - fontes.Count,
            ComSigtap: itens.Count(i => TemDigitos(i.Sol.ProcedimentoSigtapCodigo)),
            ComCid: itens.Count(i => !string.IsNullOrWhiteSpace(i.Sol.CidCodigo)),
            ComCnsProfissional: itens.Count(i => !string.IsNullOrWhiteSpace(i.CnsProfissional)),
            ComCnsPaciente: itens.Count(i => !string.IsNullOrWhiteSpace(i.Pac?.Cns)),
            ComCpfPaciente: itens.Count(i => !string.IsNullOrWhiteSpace(i.Pac?.Cpf)),
            ComSexo: itens.Count(i => i.Pac?.Sexo is Sexo.Masculino or Sexo.Feminino),
            MotivosPendencia: [.. itens.SelectMany(i => i.Motivos)
                .GroupBy(m => m).Select(g => new MotivoContagemDto(g.Key, g.Count()))
                .OrderByDescending(m => m.Quantidade)]);

        return new BpaPreviewDto(unidade.Id, unidade.Nome, unidade.Cnes, competencia, folhas, controle, est, linhas);
    }

    public async Task<(byte[] Conteudo, string NomeArquivo)> GerarArquivoAsync(Guid unidadeId, int competencia, CancellationToken ct = default)
    {
        var (unidade, itens) = await MontarAsync(unidadeId, competencia, ct);
        var fontes = itens.Where(i => i.Fonte is not null).Select(i => i.Fonte!).ToList();
        if (fontes.Count == 0)
            throw new ValidacaoException("competencia", "Nenhuma linha válida para gerar o BPA nesta competência/unidade.");

        var resultado = BpaArquivo.Gerar(fontes);
        var bytes = Encoding.Latin1.GetBytes(resultado.Texto);
        var nome = $"bpa-{unidade.Cnes}-{competencia}.txt";
        return (bytes, nome);
    }

    // ---- montagem comum ----

    private async Task<(Unidade Unidade, List<ItemBpa> Itens)> MontarAsync(Guid unidadeId, int competencia, CancellationToken ct)
    {
        var unidade = await ValidarUnidadeAsync(unidadeId, ct);
        var (inicioUtc, fimUtc) = JanelaUtc(competencia);

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        var query = db.Solicitacoes.AsNoTracking().Where(s =>
            s.UnidadeExecutanteId == unidadeId &&
            s.ChegadaConfirmadaSisreg == true &&
            s.ExcluidoEm == null &&
            s.DataAgendada != null && s.DataAgendada >= inicioUtc && s.DataAgendada < fimUtc);
        query = SolicitacaoNoEscopo.Filtrar(query, escopo);
        var sols = await query.ToListAsync(ct);

        var pacientes = await pacienteResolver.ResolverManyAsync(sols.Select(s => s.PacienteId), ct);

        var cpfs = sols.Select(s => SoDigitos(s.ProfissionalExecutanteCpf))
            .Where(c => c.Length > 0).Distinct().ToList();
        var deParas = cpfs.Count == 0
            ? new Dictionary<string, FaturamentoProfissionalCns>()
            : await db.FaturamentoProfissionaisCns.AsNoTracking()
                .Where(p => cpfs.Contains(p.Cpf)).ToDictionaryAsync(p => p.Cpf, ct);

        var ibge6 = Ibge6(unidade.CodigoIbgeCidade);

        var itens = new List<ItemBpa>(sols.Count);
        foreach (var s in sols)
        {
            var item = new ItemBpa { Sol = s };
            pacientes.TryGetValue(s.PacienteId, out var pac);
            item.Pac = pac;

            var cpfProf = SoDigitos(s.ProfissionalExecutanteCpf);
            deParas.TryGetValue(cpfProf, out var prof);
            item.CnsProfissional = prof?.Cns;
            item.Cbo = prof?.Cbo;
            item.NomeProfissional = prof?.Nome;

            var dtaten = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(s.DataAgendada!.Value, Brasilia));
            item.DataAtendimento = dtaten;

            var sigtap = SoDigitos(s.ProcedimentoSigtapCodigo);
            if (sigtap.Length == 0) item.Motivos.Add("Sem código SIGTAP do procedimento");
            if (pac is null) item.Motivos.Add("Paciente não encontrado no cadastro");
            var temDocPac = !string.IsNullOrWhiteSpace(pac?.Cns) || !string.IsNullOrWhiteSpace(pac?.Cpf);
            if (pac is not null && !temDocPac) item.Motivos.Add("Paciente sem CNS e sem CPF");
            var sexoOk = pac?.Sexo is Sexo.Masculino or Sexo.Feminino;
            if (pac is not null && !sexoOk) item.Motivos.Add("Sexo do paciente não informado");
            if (cpfProf.Length == 0) item.Motivos.Add("Sem CPF do profissional executante");
            else if (prof is null) item.Motivos.Add("CNS do profissional ainda não conciliado no e-SUS");
            else if (string.IsNullOrWhiteSpace(prof.Cbo)) item.Motivos.Add("CBO do profissional não encontrado no e-SUS");

            if (item.Motivos.Count == 0 && pac is not null)
            {
                item.Fonte = new BpaLinhaFonte(
                    SolicitacaoId: s.Id,
                    Cnes: unidade.Cnes!,
                    Competencia: competencia,
                    CnsProfissional: prof!.Cns,
                    Cbo: prof.Cbo!,
                    DataAtendimento: dtaten,
                    ProcedimentoSigtap: sigtap,
                    CnsPaciente: pac.Cns,
                    CpfPaciente: pac.Cpf,
                    Sexo: pac.Sexo == Sexo.Masculino ? 'M' : 'F',
                    Ibge6: ibge6,
                    Cid: s.CidCodigo,
                    DataNascimento: pac.DataNascimento,
                    Quantidade: 1,
                    NomePaciente: pac.Nome,
                    Raca: "99",
                    Nacionalidade: "010",
                    Cep: pac.Cep,
                    Logradouro: pac.Logradouro);
            }
            itens.Add(item);
        }
        return (unidade, itens);
    }

    private async Task<Unidade> ValidarUnidadeAsync(Guid unidadeId, CancellationToken ct)
    {
        var habilitada = await db.FaturamentoProducaoUnidades.AsNoTracking()
            .AnyAsync(x => x.UnidadeId == unidadeId && x.Habilitada, ct);
        if (!habilitada)
            throw new ValidacaoException("unidade", "Unidade não habilitada para faturamento de produção.");

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        if (!escopo.VeTudo && (escopo.SemAcesso || !escopo.Unidades.Contains(unidadeId)))
            throw new ValidacaoException("unidade", "Sem acesso a esta unidade.");

        var unidade = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(u => u.Id == unidadeId, ct)
            ?? throw new NaoEncontradoException(nameof(Unidade), unidadeId);
        if (string.IsNullOrWhiteSpace(unidade.Cnes))
            throw new ValidacaoException("unidade", "Unidade sem CNES — necessário para o BPA.");
        return unidade;
    }

    private async Task<(List<Unidade> Unidades, Dictionary<Guid, bool> Habilitada)> UnidadesDoEscopoComFlagAsync(CancellationToken ct)
    {
        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        var q = db.Unidades.AsNoTracking().Where(u => u.Ativo);
        if (!escopo.VeTudo)
        {
            if (escopo.SemAcesso) return ([], []);
            var ids = escopo.Unidades;
            q = q.Where(u => ids.Contains(u.Id));
        }
        var unidades = await q.OrderBy(u => u.Nome).ToListAsync(ct);
        var habil = await db.FaturamentoProducaoUnidades.AsNoTracking()
            .ToDictionaryAsync(x => x.UnidadeId, x => x.Habilitada, ct);
        return (unidades, habil);
    }

    // ---- helpers ----

    private static (DateTime Inicio, DateTime Fim) JanelaUtc(int competencia)
    {
        var ano = competencia / 100;
        var mes = competencia % 100;
        if (ano < 2000 || ano > 2100 || mes < 1 || mes > 12)
            throw new ValidacaoException("competencia", "Competência inválida (use AAAAMM, ex.: 202610).");
        var inicioLocal = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var fimLocal = inicioLocal.AddMonths(1);
        return (TimeZoneInfo.ConvertTimeToUtc(inicioLocal, Brasilia), TimeZoneInfo.ConvertTimeToUtc(fimLocal, Brasilia));
    }

    private static string Ibge6(string? codigo)
    {
        var d = SoDigitos(codigo);
        return d.Length >= 6 ? d[..6] : d;
    }

    private static string SoDigitos(string? v) => new((v ?? string.Empty).Where(char.IsDigit).ToArray());
    private static bool TemDigitos(string? v) => SoDigitos(v).Length > 0;
    private static string SexoStr(Sexo? s) => s switch { Sexo.Masculino => "M", Sexo.Feminino => "F", _ => string.Empty };

    private static TimeZoneInfo ResolverFuso()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
    }

    private sealed class ItemBpa
    {
        public required Solicitacao Sol { get; init; }
        public PacienteResumo? Pac { get; set; }
        public string? CnsProfissional { get; set; }
        public string? Cbo { get; set; }
        public string? NomeProfissional { get; set; }
        public DateOnly DataAtendimento { get; set; }
        public List<string> Motivos { get; } = [];
        public BpaLinhaFonte? Fonte { get; set; }
    }
}
