using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SisregWeb.Mapeamento.Dtos;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Sigtap;
using SMSMais.Core.Medicos.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Mapeamento;

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
    SmsMaisDbContext db,
    ISisregWebSessao sessao,
    ISisregUnidadeAtual unidadeAtual,
    IPractitionerFhirClient fhir,
    IMapeadorSigtapSisreg mapeadorSigtap,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<SisregMapeamentoService> logger) : ISisregMapeamentoService
{
    private const string CaminhoAjax = "/cgi-bin/sisreg_ajax";
    private const string SystemCpf = "https://fhir.saude.gov.br/sid/cpf";

    public async Task<SisregMapeamentoDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        var profissionais = await CarregarProfissionaisAsync(unidade.Id, rastrear: false, cancellationToken);

        var codigos = profissionais.SelectMany(p => p.Procedimentos).Select(p => p.Codigo).ToArray();
        var catalogo = await mapeadorSigtap.ObterCatalogoAsync(codigos, cancellationToken);

        return ParaDto(unidade, profissionais, catalogo);
    }

    /// <summary>
    /// Um procedimento com o estado do seu de-para. Sem SIGTAP confirmado ele é exibido como
    /// pendente e a varredura o pula — habilitar não basta.
    /// </summary>
    private static SisregProcedimentoDto ParaProcedimentoDto(
        SisregProcedimentoProfissional procedimento,
        IReadOnlyDictionary<string, ProcedimentoCatalogoInfo> catalogo)
    {
        var noCatalogo = catalogo.GetValueOrDefault(procedimento.Codigo);

        return new SisregProcedimentoDto(
            procedimento.Id, procedimento.Codigo, procedimento.Nome, procedimento.Habilitado,
            procedimento.Grupo, procedimento.Ausente,
            DeParaId: noCatalogo?.DeParaId,
            EnviarConfirmacao: procedimento.EnviarConfirmacao);
    }

    public async Task<SisregMapeamentoAtualizacaoDto> AtualizarAsync(CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        return await AtualizarNoContextoAsync(unidade, usuarioAtual.UsuarioId, null, cancellationToken);
    }

    /// <summary>
    /// Núcleo do <see cref="AtualizarAsync"/> com a unidade e o autor <b>explícitos</b> e um gancho
    /// opcional antes de cada requisição ao SISREG. É a porta que o motor em lote usa para
    /// reconciliar qualquer unidade fora de uma request — sem <c>X-Unidade-Id</c> nem usuário
    /// logado. O gancho serve ao lote para respeitar o teto de requisições/hora (o mapeamento e a
    /// varredura dividem o mesmo orçamento anti-robô do SISREG).
    /// </summary>
    public async Task<SisregMapeamentoAtualizacaoDto> AtualizarNoContextoAsync(
        Unidade unidade,
        Guid? usuarioId,
        Func<CancellationToken, Task>? antesDeCadaRequisicao,
        CancellationToken cancellationToken = default,
        TimeSpan? ttlProcedimentos = null)
    {
        if (string.IsNullOrWhiteSpace(unidade.Cnes))
        {
            throw new ValidacaoException(
                "sisreg.unidade_sem_cnes",
                $"A unidade '{unidade.Nome}' não tem CNES cadastrado — sem ele não é possível "
                + "consultar os profissionais dela no SISREG.");
        }

        // Sem double-check de unidade: a credencial em uso enxerga todas as unidades, então o
        // CNES da sessão não diz nada sobre o que estamos mapeando. Quem delimita a unidade é o
        // próprio AJAX_UPS abaixo — o SISREG só devolve profissionais daquele CNES.
        var requisicoes = 0;
        var cnes = SoDigitos(unidade.Cnes!);

        if (antesDeCadaRequisicao is not null) await antesDeCadaRequisicao(cancellationToken);
        var xmlProfissionais = await sessao.GetAsync(
            CaminhoAjax,
            new Dictionary<string, string> { ["BUSCA"] = "PROFISSIONAIS_POR_UPS", ["AJAX_UPS"] = cnes },
            cancellationToken,
            SemLinhas);
        requisicoes++;

        var doSisreg = SisregAjaxParser.LerLinhas(xmlProfissionais);
        var existentes = await CarregarProfissionaisAsync(unidade.Id, rastrear: true, cancellationToken);

        // <ROOT/> vazio tem DUAS causas com a mesma cara, e o que decide é o que já sabemos da
        // unidade — sem gastar requisição nenhuma para descobrir.
        if (doSisreg.Count == 0)
        {
            if (existentes.Count > 0)
            {
                // Tinha gente e agora não tem: quase sempre é sessão derrubada ou anti-bot. Seguir
                // aqui seria pior do que parar — o trecho abaixo marcaria os 113 profissionais da
                // unidade como AUSENTES, e ausente sai da varredura e da sincronização com o hub.
                // Uma unidade inteira sumiria da operação por causa de uma resposta vazia.
                throw new ValidacaoException(
                    "sisreg.lista_vazia",
                    $"O SISREG não devolveu nenhum profissional para '{unidade.Nome}', mas há "
                    + $"{existentes.Count} cadastrados aqui. Isso normalmente significa que a sessão "
                    + "do operador foi derrubada (o SISREG aceita uma sessão por operador) ou que o "
                    + "acesso está bloqueado por CAPTCHA. Nada foi alterado. Teste a credencial do "
                    + "SISREG e tente de novo.");
            }

            // Nunca teve ninguém: a unidade existe no SISREG mas não é EXECUTANTE — é o caso da
            // central de regulação, que aparece no combo de unidades e não tem agenda de
            // profissional. Não é erro, e insistir nela todo dia só gasta orçamento.
            logger.LogInformation(
                "SISREG: unidade {Unidade} não tem profissional executante no SISREG.", unidade.Nome);

            return new SisregMapeamentoAtualizacaoDto(
                0, 0, 0, 0, 0, 0, requisicoes,
                $"'{unidade.Nome}' não tem profissional executante no SISREG — é o esperado em "
                + "unidade que não executa agenda (central de regulação, por exemplo).");
        }
        var porCpf = existentes.ToDictionary(p => p.Cpf, StringComparer.Ordinal);
        var agora = DateTime.UtcNow;

        var novos = 0;
        var procedimentosEncontrados = 0;
        var procedimentosNovos = 0;
        var puladosPorTtl = 0;
        var vistosAgora = new HashSet<string>(StringComparer.Ordinal);

        // Código do SISREG → nome, para alimentar o catálogo do de-para com o SIGTAP. A varredura
        // da agenda não informa SIGTAP, e sem ele a solicitação nasceria sem categoria; catalogar
        // aqui é de graça (os procedimentos já vieram nesta mesma requisição).
        var paraDePara = new Dictionary<string, string>(StringComparer.Ordinal);

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
                    CriadoPor = usuarioId,
                };
                db.SisregProfissionaisUnidade.Add(profissional);
                porCpf[cpf] = profissional;
                novos++;
            }
            else
            {
                profissional.AtualizadoEm = agora;
                profissional.AtualizadoPor = usuarioId;
            }

            profissional.Nome = linha.Descricao;
            profissional.VistoEm = agora;
            profissional.Ausente = false;

            // Aqui está o maior gasto do mapeamento: UMA requisição por profissional. Quem já foi
            // buscado há pouco e continua com procedimentos não é rebuscado — a lista acima (1
            // requisição para a unidade toda) já confirmou que ele segue lá, e procedimento de
            // médico é cadastro que muda em meses, não em horas. Sem TTL (uso interativo) busca
            // sempre: quando o operador clica em "atualizar", ele quer o estado de agora.
            if (ttlProcedimentos is { } ttl
                && profissional.ProcedimentosVistosEm is { } visto
                && agora - visto < ttl
                && profissional.Procedimentos.Count > 0)
            {
                puladosPorTtl++;
                continue;
            }

            if (antesDeCadaRequisicao is not null) await antesDeCadaRequisicao(cancellationToken);
            var xmlProcedimentos = await sessao.GetAsync(
                CaminhoAjax,
                new Dictionary<string, string>
                {
                    ["BUSCA"] = "PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS",
                    ["AJAX_CPF"] = cpf,
                    ["AJAX_UPS"] = cnes,
                },
                cancellationToken,
                SemLinhas);
            requisicoes++;

            var procedimentos = SisregAjaxParser.LerLinhas(xmlProcedimentos);
            procedimentosEncontrados += procedimentos.Count;
            procedimentosNovos += ReconciliarProcedimentos(profissional, procedimentos, agora);
            profissional.ProcedimentosVistosEm = agora;

            foreach (var procedimento in procedimentos)
            {
                var codigo = procedimento.Codigo.Trim();
                if (codigo.Length > 0) paraDePara[codigo] = procedimento.Descricao;
            }
        }

        // Sumiu do SISREG: não apagamos (perderia habilitação e vínculo FHIR) — marcamos.
        var ausentes = 0;
        foreach (var profissional in existentes.Where(p => !vistosAgora.Contains(p.Cpf) && !p.Ausente))
        {
            profissional.Ausente = true;
            profissional.AtualizadoEm = agora;
            profissional.AtualizadoPor = usuarioId;
            ausentes++;
        }

        // Contado em memória: as marcações de ausente acima ainda não foram gravadas.
        var procedimentosAusentes = porCpf.Values.SelectMany(p => p.Procedimentos).Count(x => x.Ausente);

        await db.SaveChangesAsync(cancellationToken);

        // Depois do SaveChanges: o catálogo do de-para é global e não deve prender a transação do
        // mapeamento da unidade. Falhar aqui não pode desfazer o mapeamento que já foi gravado.
        await mapeadorSigtap.RegistrarVistosAsync(
            [.. paraDePara.Select(p => new ProcedimentoVisto(
                p.Key, p.Value, p.Key.EndsWith("000", StringComparison.Ordinal)))],
            cancellationToken);

        logger.LogInformation(
            "SISREG: mapeamento da unidade {Unidade} atualizado — {Total} profissionais ({Novos} novos), "
            + "{Pulados} dentro do TTL, {Req} requisições.",
            unidade.Nome, doSisreg.Count, novos, puladosPorTtl, requisicoes);

        var economia = puladosPorTtl > 0
            ? $" ({puladosPorTtl} profissionais já estavam atualizados e não foram consultados de novo)"
            : string.Empty;

        return new SisregMapeamentoAtualizacaoDto(
            doSisreg.Count, novos, ausentes,
            procedimentosEncontrados, procedimentosNovos, procedimentosAusentes,
            requisicoes,
            $"Mapeamento atualizado a partir do SISREG ({unidade.Nome}): {doSisreg.Count} profissionais, "
            + $"{procedimentosEncontrados} procedimentos, em {requisicoes} requisições{economia}.",
            puladosPorTtl);
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

    public async Task<int> AlternarEnvioConfirmacaoAsync(
        Guid procedimentoId, bool enviar, CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        var procedimento = await db.SisregProcedimentosProfissional
                .Include(x => x.Profissional)
                .FirstOrDefaultAsync(
                    x => x.Id == procedimentoId && x.Profissional!.UnidadeId == unidade.Id, cancellationToken)
            ?? throw new NaoEncontradoException("Procedimento do mapeamento SISREG", procedimentoId);

        // Aplica a TODAS as linhas do mesmo procedimento NESTA unidade. O mesmo código costuma
        // aparecer sob vários profissionais, e a decisão de avisar o paciente é do procedimento na
        // unidade — não do par com o profissional. Sem isto o operador desligaria o aviso num
        // profissional e continuaria enviando pelos outros, sem nada indicar.
        var irmaos = await db.SisregProcedimentosProfissional
            .Include(x => x.Profissional)
            .Where(x => x.Codigo == procedimento.Codigo && x.Profissional!.UnidadeId == unidade.Id)
            .ToListAsync(cancellationToken);

        foreach (var irmao in irmaos) irmao.EnviarConfirmacao = enviar;

        await db.SaveChangesAsync(cancellationToken);
        return irmaos.Count;
    }

    public async Task AlternarProcedimentosDoProfissionalAsync(
        Guid profissionalId, bool habilitados, bool enviarConfirmacao,
        CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        var profissional = await db.SisregProfissionaisUnidade
                .Include(x => x.Procedimentos)
                .FirstOrDefaultAsync(x => x.Id == profissionalId && x.UnidadeId == unidade.Id, cancellationToken)
            ?? throw new NaoEncontradoException("Profissional do mapeamento SISREG", profissionalId);

        // Espelha o que o operador faria clicando um a um: o médico e cada procedimento dele
        // seguem o mesmo habilita/desabilita. Só automatiza os cliques — nenhuma semântica muda.
        profissional.Habilitado = habilitados;
        profissional.AtualizadoEm = DateTime.UtcNow;
        profissional.AtualizadoPor = usuarioAtual.UsuarioId;
        foreach (var proc in profissional.Procedimentos) proc.Habilitado = habilitados;

        // O zap continua sendo decisão do procedimento NA UNIDADE (ADR-0040): aplica a todas as
        // linhas dos mesmos códigos na unidade, não só às deste profissional — idêntico a alternar
        // o zap procedimento a procedimento.
        var codigos = profissional.Procedimentos.Select(p => p.Codigo).Distinct().ToList();
        if (codigos.Count > 0)
        {
            var irmaos = await db.SisregProcedimentosProfissional
                .Include(x => x.Profissional)
                .Where(x => codigos.Contains(x.Codigo) && x.Profissional!.UnidadeId == unidade.Id)
                .ToListAsync(cancellationToken);
            foreach (var irmao in irmaos) irmao.EnviarConfirmacao = enviarConfirmacao;
        }

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

    public async Task<AlternarTudoDaUnidadeDto> AlternarTudoDaUnidadeAsync(
        bool habilitados, bool? enviarConfirmacao, CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);

        // Só o que o SISREG ainda reconhece. Reabilitar em massa quem já sumiu de lá reintroduziria
        // combinação morta na varredura — e o custo dela é exatamente o que este botão tenta poupar.
        var profissionais = await db.SisregProfissionaisUnidade
            .Include(x => x.Procedimentos)
            .Where(x => x.UnidadeId == unidade.Id && !x.Ausente)
            .ToListAsync(cancellationToken);

        if (profissionais.Count == 0)
        {
            throw new ValidacaoException(
                "sisreg.mapeamento_vazio",
                $"A unidade '{unidade.Nome}' ainda não tem mapeamento do SISREG. Clique em "
                + "\"Atualizar mapeamento\" para buscar os profissionais antes de habilitar tudo.");
        }

        var agora = DateTime.UtcNow;
        var procedimentosAfetados = 0;

        foreach (var profissional in profissionais)
        {
            profissional.Habilitado = habilitados;
            profissional.AtualizadoEm = agora;
            profissional.AtualizadoPor = usuarioAtual.UsuarioId;

            foreach (var procedimento in profissional.Procedimentos.Where(p => !p.Ausente))
            {
                procedimento.Habilitado = habilitados;
                // O zap só muda se pedirem explicitamente. Este botão liga o SINCRONISMO; arrastar
                // o aviso por WhatsApp junto apagaria de uma vez a escolha feita procedimento a
                // procedimento — e o aviso já tem controles próprios (mestre da unidade e caixinha
                // de cada procedimento, ADR-0040).
                if (enviarConfirmacao is { } zap) procedimento.EnviarConfirmacao = zap;
                procedimentosAfetados++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var combinacoes = habilitados ? procedimentosAfetados : 0;
        var recorte = await db.SisregVarreduraAgendas.AsNoTracking()
            .Where(x => x.UnidadeId == unidade.Id)
            .Select(x => (bool?)x.RecorteUnidadeInteira)
            .FirstOrDefaultAsync(cancellationToken) ?? false;

        logger.LogInformation(
            "SISREG: unidade {Unidade} — {Acao} {Profs} profissionais e {Procs} procedimentos de uma vez.",
            unidade.Nome, habilitados ? "habilitados" : "desabilitados",
            profissionais.Count, procedimentosAfetados);

        var zapDito = enviarConfirmacao switch
        {
            true => ", com aviso por WhatsApp ligado",
            false => ", com aviso por WhatsApp desligado",
            null => string.Empty, // não mexeu no zap — não anuncia o que não mudou
        };

        var mensagem = habilitados
            ? $"{profissionais.Count} profissionais e {procedimentosAfetados} procedimentos habilitados"
              + zapDito
              + (recorte
                  ? ". A varredura desta unidade puxa a agenda inteira numa requisição, então isto não muda o custo dela."
                  : $". A varredura desta unidade passa a custar {combinacoes} requisições.")
            : $"{profissionais.Count} profissionais e {procedimentosAfetados} procedimentos desabilitados.";

        return new AlternarTudoDaUnidadeDto(
            profissionais.Count, procedimentosAfetados, combinacoes, recorte, mensagem);
    }

    public async Task<SisregSincronizacaoFhirDto> SincronizarFhirAsync(CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        return await SincronizarFhirNoContextoAsync(unidade, usuarioAtual.UsuarioId, cancellationToken);
    }

    /// <summary>
    /// Núcleo do <see cref="SincronizarFhirAsync"/> com a unidade e o autor <b>explícitos</b>, para
    /// o motor em lote rodar fora de uma request. Fala com o hub FHIR (não com o SISREG), então não
    /// consome o orçamento anti-robô — por isso não tem gancho de throttle.
    /// </summary>
    public async Task<SisregSincronizacaoFhirDto> SincronizarFhirNoContextoAsync(
        Unidade unidade, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
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
                profissional.AtualizadoPor = usuarioId;
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

    /// <summary>
    /// Reconcilia os procedimentos de um profissional; devolve quantos são novos.
    ///
    /// <para><b>O procedimento novo entra pelo DbSet, não só pela navegação.</b> Ele nasce com
    /// <c>Id</c> já preenchido, e para o EF "chave preenchida" em entidade descoberta pela
    /// navegação de um pai <i>já existente</i> significa <c>Modified</c> — vira UPDATE de uma
    /// linha que nunca foi inserida, que acerta 0 linhas e estoura
    /// <c>DbUpdateConcurrencyException</c>. O middleware traduz isso como "alterado por outra
    /// requisição concorrente", acusando uma concorrência que não existe. Enquanto o profissional
    /// também era novo (pai <c>Added</c>) o EF cascateava <c>Added</c> e o defeito ficava
    /// escondido: só apareceu quando o SISREG passou a trazer procedimento novo em profissional
    /// antigo.</para>
    /// </summary>
    private int ReconciliarProcedimentos(
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
                db.SisregProcedimentosProfissional.Add(procedimento); // ver nota do método
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

    /// <summary>
    /// Resposta do AJAX sem nenhuma linha — o sintoma de sessão derrubada que o detector de HTML
    /// não vê, porque aqui o SISREG devolve <c>&lt;ROOT/&gt;</c> e não a tela de login. Entregue à
    /// sessão para ela relogar e repetir; se voltar vazio de novo, aí o vazio é real.
    ///
    /// <para>Vale também para a lista de procedimentos de um profissional: sem isto, uma sessão
    /// que caísse no meio do mapeamento faria todos os profissionais restantes voltarem com zero
    /// procedimentos — e a reconciliação marcaria os procedimentos deles como <c>Ausente</c>, em
    /// silêncio, como se o SISREG os tivesse descadastrado.</para>
    /// </summary>
    private static bool SemLinhas(string xml) => SisregAjaxParser.LerLinhas(xml).Count == 0;

    private static string SoDigitos(string valor) => new([.. valor.Where(char.IsDigit)]);

    private static SisregMapeamentoDto ParaDto(
        Unidade unidade,
        List<SisregProfissionalUnidade> profissionais,
        IReadOnlyDictionary<string, ProcedimentoCatalogoInfo> catalogo)
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
                    .Select(x => ParaProcedimentoDto(x, catalogo))]))]);
    }
}
