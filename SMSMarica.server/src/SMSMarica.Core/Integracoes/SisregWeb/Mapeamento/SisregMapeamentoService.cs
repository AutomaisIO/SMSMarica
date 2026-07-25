using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes.SisregWeb.Mapeamento.Dtos;
using SMSMarica.Core.Medicos.Fhir;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Core.Integracoes.SisregWeb.Mapeamento;

/// <summary>
/// Constrói e mantém a "verdade" do SISREG para uma unidade: quais profissionais executam
/// nela e quais procedimentos cada um tem cadastrado.
///
/// <para><b>Por que isso existe:</b> a agenda do SISREG só é consultável informando
/// unidade + profissional + procedimento (obrigatórios no servidor). Materializar a agenda é
/// varrer esse produto cartesiano — e no CDT, por exemplo, 92 profissionais rendem 256 pares,
/// mas a maioria não tem agenda alguma. Como o SISREG passa a exigir CAPTCHA por volume de
/// acessos, gastar requisição com quem não interessa é o que quebra a varredura. Daí os
/// checkboxes: eles definem o custo de cada varredura.</para>
/// </summary>
public sealed class SisregMapeamentoService(
    SmsMaricaDbContext db,
    ISisregWebSessao sessao,
    ISisregUnidadeAtual unidadeAtual,
    IPractitionerFhirClient fhir,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<SisregMapeamentoService> logger) : ISisregMapeamentoService
{
    private const string CaminhoAjax = "/cgi-bin/sisreg_ajax";
    private const string SystemCpf = "https://fhir.saude.gov.br/sid/cpf";

    public async Task<SisregMapeamentoDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        var profissionais = await CarregarProfissionaisAsync(unidade.Id, rastrear: false, cancellationToken);
        return ParaDto(unidade, profissionais);
    }

    public async Task<SisregMapeamentoAtualizacaoDto> AtualizarAsync(CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(unidade.Cnes))
        {
            throw new ValidacaoException(
                "sisreg.unidade_sem_cnes",
                $"A unidade '{unidade.Nome}' não tem CNES cadastrado — sem ele não é possível "
                + "consultar os profissionais dela no SISREG.");
        }

        // Double-check: a sessão do SISREG tem que ser desta unidade, senão mapearíamos a agenda
        // de outra unidade sem perceber.
        var info = await sessao.ObterSessaoInfoAsync(unidade.Id, cancellationToken);
        GarantirUnidadeConfere(unidade, info);

        var requisicoes = 1;
        var cnes = SoDigitos(unidade.Cnes!);

        var xmlProfissionais = await sessao.GetAsync(
            unidade.Id, CaminhoAjax,
            new Dictionary<string, string> { ["BUSCA"] = "PROFISSIONAIS_POR_UPS", ["AJAX_UPS"] = cnes },
            cancellationToken);
        requisicoes++;

        var doSisreg = SisregAjaxParser.LerLinhas(xmlProfissionais);
        if (doSisreg.Count == 0)
        {
            // <ROOT/> vazio não é "unidade sem profissionais": é sessão derrubada ou anti-bot.
            throw new ValidacaoException(
                "sisreg.lista_vazia",
                "O SISREG não devolveu nenhum profissional para esta unidade. Isso normalmente "
                + "significa que a sessão do operador foi derrubada (o SISREG aceita uma sessão por "
                + "operador) ou que o acesso está bloqueado por CAPTCHA. Teste a credencial da "
                + "unidade e tente de novo.");
        }

        var existentes = await CarregarProfissionaisAsync(unidade.Id, rastrear: true, cancellationToken);
        var porCpf = existentes.ToDictionary(p => p.Cpf, StringComparer.Ordinal);
        var agora = DateTime.UtcNow;

        var novos = 0;
        var procedimentosEncontrados = 0;
        var procedimentosNovos = 0;
        var vistosAgora = new HashSet<string>(StringComparer.Ordinal);

        foreach (var linha in doSisreg)
        {
            var cpf = SoDigitos(linha.Codigo);
            if (cpf.Length == 0) continue;
            vistosAgora.Add(cpf);

            if (!porCpf.TryGetValue(cpf, out var profissional))
            {
                profissional = new SisregProfissionalUnidade
                {
                    Id = Guid.NewGuid(),
                    UnidadeId = unidade.Id,
                    Cpf = cpf,
                    Habilitado = false, // novo entra desligado: quem decide o custo da varredura é o operador
                    CriadoEm = agora,
                    CriadoPor = usuarioAtual.UsuarioId,
                };
                db.SisregProfissionaisUnidade.Add(profissional);
                porCpf[cpf] = profissional;
                novos++;
            }
            else
            {
                profissional.AtualizadoEm = agora;
                profissional.AtualizadoPor = usuarioAtual.UsuarioId;
            }

            profissional.Nome = linha.Descricao;
            profissional.VistoEm = agora;
            profissional.Ausente = false;

            var xmlProcedimentos = await sessao.GetAsync(
                unidade.Id, CaminhoAjax,
                new Dictionary<string, string>
                {
                    ["BUSCA"] = "PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS",
                    ["AJAX_CPF"] = cpf,
                    ["AJAX_UPS"] = cnes,
                },
                cancellationToken);
            requisicoes++;

            var procedimentos = SisregAjaxParser.LerLinhas(xmlProcedimentos);
            procedimentosEncontrados += procedimentos.Count;
            procedimentosNovos += ReconciliarProcedimentos(profissional, procedimentos, agora);
        }

        // Sumiu do SISREG: não apagamos (perderia habilitação e vínculo FHIR) — marcamos.
        var ausentes = 0;
        foreach (var profissional in existentes.Where(p => !vistosAgora.Contains(p.Cpf) && !p.Ausente))
        {
            profissional.Ausente = true;
            profissional.AtualizadoEm = agora;
            profissional.AtualizadoPor = usuarioAtual.UsuarioId;
            ausentes++;
        }

        // Contado em memória: as marcações de ausente acima ainda não foram gravadas.
        var procedimentosAusentes = porCpf.Values.SelectMany(p => p.Procedimentos).Count(x => x.Ausente);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SISREG: mapeamento da unidade {Unidade} atualizado — {Total} profissionais ({Novos} novos), {Req} requisições.",
            unidade.Nome, doSisreg.Count, novos, requisicoes);

        return new SisregMapeamentoAtualizacaoDto(
            doSisreg.Count, novos, ausentes,
            procedimentosEncontrados, procedimentosNovos, procedimentosAusentes,
            requisicoes,
            $"Mapeamento atualizado a partir do SISREG ({info.UnidadeNome}): {doSisreg.Count} profissionais, "
            + $"{procedimentosEncontrados} procedimentos, em {requisicoes} requisições.");
    }

    public async Task AlternarProfissionalAsync(
        Guid profissionalId, bool habilitado, CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        var profissional = await db.SisregProfissionaisUnidade
                .FirstOrDefaultAsync(x => x.Id == profissionalId && x.UnidadeId == unidade.Id, cancellationToken)
            ?? throw new NaoEncontradoException("Profissional do mapeamento SISREG", profissionalId);

        profissional.Habilitado = habilitado;
        profissional.AtualizadoEm = DateTime.UtcNow;
        profissional.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AlternarProcedimentoAsync(
        Guid procedimentoId, bool habilitado, CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        var procedimento = await db.SisregProcedimentosProfissional
                .Include(x => x.Profissional)
                .FirstOrDefaultAsync(
                    x => x.Id == procedimentoId && x.Profissional!.UnidadeId == unidade.Id, cancellationToken)
            ?? throw new NaoEncontradoException("Procedimento do mapeamento SISREG", procedimentoId);

        procedimento.Habilitado = habilitado;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AlternarProfissionaisEmLoteAsync(
        IReadOnlyList<Guid> ids, bool habilitado, CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        if (ids.Count == 0) return;

        var profissionais = await db.SisregProfissionaisUnidade
            .Where(x => x.UnidadeId == unidade.Id && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var agora = DateTime.UtcNow;
        foreach (var profissional in profissionais)
        {
            profissional.Habilitado = habilitado;
            profissional.AtualizadoEm = agora;
            profissional.AtualizadoPor = usuarioAtual.UsuarioId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SisregSincronizacaoFhirDto> SincronizarFhirAsync(CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);

        var habilitados = await db.SisregProfissionaisUnidade
            .Where(x => x.UnidadeId == unidade.Id && x.Habilitado && !x.Ausente)
            .ToListAsync(cancellationToken);

        var criados = 0;
        var vinculados = 0;
        var jaSincronizados = 0;
        var erros = new List<string>();
        var agora = DateTime.UtcNow;

        foreach (var profissional in habilitados)
        {
            if (profissional.PractitionerId is not null)
            {
                jaSincronizados++;
                continue;
            }

            try
            {
                // Dedup por CPF: o profissional pode já existir no hub (cadastrado como médico
                // ou importado de um PEP). Nesse caso só vinculamos, sem duplicar identidade.
                var bundle = await fhir.BuscarAsync(identifier: profissional.Cpf, ct: cancellationToken);
                var existente = bundle.Entry?
                    .Select(e => e.Resource)
                    .OfType<Practitioner>()
                    .FirstOrDefault();

                if (existente is not null && Guid.TryParse(existente.Id, out var idExistente))
                {
                    profissional.PractitionerId = idExistente;
                    vinculados++;
                }
                else
                {
                    var criado = await fhir.CriarAsync(ConstruirPractitioner(profissional), cancellationToken);
                    if (Guid.TryParse(criado.Id, out var idNovo))
                    {
                        profissional.PractitionerId = idNovo;
                        criados++;
                    }
                    else
                    {
                        erros.Add($"{profissional.Nome}: o hub não devolveu um id válido.");
                        continue;
                    }
                }

                profissional.SincronizadoEm = agora;
                profissional.AtualizadoEm = agora;
                profissional.AtualizadoPor = usuarioAtual.UsuarioId;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Um profissional problemático não pode abortar a sincronização inteira.
                logger.LogWarning(ex, "SISREG: falha ao sincronizar profissional {Cpf} com o hub FHIR.", profissional.Cpf);
                erros.Add($"{profissional.Nome}: {ex.Message}");
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new SisregSincronizacaoFhirDto(
            habilitados.Count, criados, vinculados, jaSincronizados, erros,
            $"{criados} criados no hub, {vinculados} vinculados a profissionais já existentes, "
            + $"{jaSincronizados} já sincronizados"
            + (erros.Count > 0 ? $", {erros.Count} com erro." : "."));
    }

    // ------------------------------------------------------------------ interno

    /// <summary>
    /// Practitioner mínimo e válido em R4: o SISREG só entrega CPF e nome — sem conselho, sem
    /// demografia. Nada de inventar <c>qualification</c> que o SISREG não afirmou.
    /// </summary>
    private static Practitioner ConstruirPractitioner(SisregProfissionalUnidade profissional) => new()
    {
        Active = true,
        Identifier = [new Identifier(SystemCpf, profissional.Cpf)],
        Name = [new HumanName { Use = HumanName.NameUse.Official, Text = profissional.Nome }],
    };

    /// <summary>Reconcilia os procedimentos de um profissional; devolve quantos são novos.</summary>
    private static int ReconciliarProcedimentos(
        SisregProfissionalUnidade profissional, IReadOnlyList<SisregAjaxParser.Linha> doSisreg, DateTime agora)
    {
        var novos = 0;
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var linha in doSisreg)
        {
            var codigo = linha.Codigo.Trim();
            vistos.Add(codigo);

            var procedimento = profissional.Procedimentos.FirstOrDefault(p => p.Codigo == codigo);
            if (procedimento is null)
            {
                procedimento = new SisregProcedimentoProfissional
                {
                    Id = Guid.NewGuid(),
                    ProfissionalId = profissional.Id,
                    Codigo = codigo,
                    Habilitado = false,
                };
                profissional.Procedimentos.Add(procedimento);
                novos++;
            }

            procedimento.Nome = linha.Descricao;
            // Código terminado em 000 é "GRUPO - X": a consulta dele já traz os itens individuais.
            procedimento.Grupo = codigo.EndsWith("000", StringComparison.Ordinal);
            procedimento.VistoEm = agora;
            procedimento.Ausente = false;
        }

        foreach (var procedimento in profissional.Procedimentos.Where(p => !vistos.Contains(p.Codigo)))
        {
            procedimento.Ausente = true;
        }

        return novos;
    }

    private async Task<List<SisregProfissionalUnidade>> CarregarProfissionaisAsync(
        Guid unidadeId, bool rastrear, CancellationToken cancellationToken)
    {
        var query = db.SisregProfissionaisUnidade
            .Include(x => x.Procedimentos)
            .Where(x => x.UnidadeId == unidadeId);

        if (!rastrear) query = query.AsNoTracking();

        return await query.OrderBy(x => x.Nome).ToListAsync(cancellationToken);
    }

    private static void GarantirUnidadeConfere(Unidade unidade, SisregSessaoInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Cnes)
            || !string.Equals(SoDigitos(unidade.Cnes ?? string.Empty), SoDigitos(info.Cnes), StringComparison.Ordinal))
        {
            throw new ValidacaoException(
                "sisreg.unidade_divergente",
                $"A sessão do SISREG está em '{info.UnidadeNome}' (CNES {info.Cnes}), mas a unidade "
                + $"selecionada é '{unidade.Nome}' (CNES {unidade.Cnes}). Cadastre a credencial do "
                + "operador desta unidade na Configuração SISREG antes de mapear.");
        }
    }

    private static string SoDigitos(string valor) => new([.. valor.Where(char.IsDigit)]);

    private static SisregMapeamentoDto ParaDto(Unidade unidade, List<SisregProfissionalUnidade> profissionais)
    {
        var visiveis = profissionais.Where(p => !p.Ausente || p.Habilitado).ToList();
        var procedimentos = visiveis.SelectMany(p => p.Procedimentos).ToList();

        return new SisregMapeamentoDto(
            unidade.Id,
            unidade.Nome,
            unidade.Cnes,
            profissionais.Count == 0 ? null : profissionais.Max(p => p.VistoEm),
            visiveis.Count,
            visiveis.Count(p => p.Habilitado),
            procedimentos.Count,
            procedimentos.Count(p => p.Habilitado),
            visiveis.Where(p => p.Habilitado).Sum(p => p.Procedimentos.Count(x => x.Habilitado)),
            [.. visiveis.Select(p => new SisregProfissionalDto(
                p.Id, p.Cpf, p.Nome, p.Habilitado, p.PractitionerId, p.SincronizadoEm, p.Ausente,
                [.. p.Procedimentos
                    .Where(x => !x.Ausente || x.Habilitado)
                    .OrderBy(x => x.Nome)
                    .Select(x => new SisregProcedimentoDto(x.Id, x.Codigo, x.Nome, x.Habilitado, x.Grupo, x.Ausente))]))]);
    }
}
