using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.KlinikosWeb.Cid;
using SMSMais.Core.Integracoes.KlinikosWeb.Fhir;
using SMSMais.Core.Integracoes.KlinikosWeb.Varredura;
using SMSMais.Core.Integracoes.Pep.Estrategias.Klinikos;
using SMSMais.Core.Integracoes.Pep.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities.KlinikosWeb;

namespace SMSMais.Core.Integracoes.KlinikosWeb.Escrita;

/// <summary>Contagens da escrita da espinha (sem PII).</summary>
public sealed record ResumoEscrita(
    string Slug, DateOnly Dia, int Boletins, int Encounters, int Conditions,
    int CidNaoMapeado, int Enfileirados);

/// <summary>
/// ESCRITA da espinha do Klinikos web no hub + BACKFILL do buraco da migração do Conde. Grava
/// Encounter/Condition/identidade idempotentes por identifier (mesma forma do SQL) e ENFILEIRA o
/// boletim para o deep (narrativa/prescrição/vitais por tela, drenados depois).
///
/// <para><b>INERTE por padrão e por segurança:</b></para>
/// <list type="number">
///   <item>só grava se <c>KlinikosWeb:EscritaHabilitada = true</c> (padrão false);</item>
///   <item>só grava onde o web é a fonte primária — <b>Conde</b> (as UPAs têm dono SQL; gravar
///     ali duplicaria a pessoa/boletim);</item>
///   <item>só roda por gatilho explícito (endpoint RBAC) — nenhum job dispara sozinho.</item>
/// </list>
///
/// <para>TODO(identidade): o paciente é gravado FINO pela chave local = prontuário; a união por
/// CPF (canônica, via <c>UpsertCanonicoPep</c>) depende de trazer o CPF pelo cadastro (rel. 21) ou
/// pelo deep. TODO(org): resolver a Organization por CNES. TODO(desfecho): status/desfecho pelo 526.</para>
/// </summary>
public interface IKlinikosWebEscritaService
{
    Task<ResumoEscrita> GravarEspinhaAsync(string slug, DateOnly dia, CancellationToken ct);

    /// <summary>Backfill dia a dia, inclusivo. Para o Conde: <c>de</c>=08/08/2026, <c>ate</c>=hoje.</summary>
    Task<IReadOnlyList<ResumoEscrita>> BackfillAsync(string slug, DateOnly de, DateOnly ate, CancellationToken ct);
}

public sealed class KlinikosWebEscritaService(
    IKlinikosWebSincronizacaoService sincronizacao,
    IKlinikosWebFonteResolver resolver,
    IHubFhirEscritor hub,
    ICidDeParaService cid,
    SmsMaisDbContext db,
    IOptions<KlinikosWebOpcoes> opcoes,
    ILogger<KlinikosWebEscritaService> logger) : IKlinikosWebEscritaService
{
    public async Task<ResumoEscrita> GravarEspinhaAsync(string slug, DateOnly dia, CancellationToken ct)
    {
        var fonte = await resolver.ResolverAsync(slug, ct);
        GarantirPodeEscrever(slug, fonte.WebPrimaria);

        var mapper = new KlinikosWebFhirMapper(fonte.Instancia.Slug, fonte.MetaSource, cid);
        var unidCodigo = fonte.Instancia.UnidCodigo;

        var espinha = await sincronizacao.MontarEspinhaAsync(slug, dia, ct);
        var cids = await sincronizacao.PuxarCidPorBoletimAsync(slug, dia, ct);

        int encs = 0, conds = 0, naoMapeado = 0, enfileirados = 0;

        foreach (var e in espinha)
        {
            ct.ThrowIfCancellationRequested();

            // Paciente FINO (chave local = prontuário) — idempotente por identifier.
            var paciente = mapper.MontarPaciente(e);
            var identPac = paciente.Identifier.First(i => i.System == KlinikosFhirMapper.IdentPaciente);
            var pacSalvo = await hub.UpsertPorIdentifierAsync(paciente, identPac.System!, identPac.Value!, ct);
            var pacRef = $"Patient/{pacSalvo.Id}";

            // Encounter.
            var enc = mapper.MontarEncounter(e, pacRef, organizationRef: null, unidCodigo);
            var identEnc = enc.Identifier.First(i => i.System == KlinikosFhirMapper.IdentBoletim);
            var encSalvo = await hub.UpsertPorIdentifierAsync(enc, identEnc.System!, identEnc.Value!, ct);
            encs++;

            // Condition (CID via de-para).
            cids.TryGetValue(e.SpaCodigo, out var cidTexto);
            if (mapper.MontarCondition(e.SpaCodigo, cidTexto, pacRef, $"Encounter/{encSalvo.Id}", out var mapeado) is { } cond)
            {
                var identCond = cond.Identifier.First();
                await hub.UpsertPorIdentifierAsync(cond, identCond.System!, identCond.Value!, ct);
                conds++;
                if (!mapeado) naoMapeado++;
            }

            if (await EnfileirarDeepAsync(slug, e.SpaCodigo, unidCodigo, ct)) enfileirados++;
        }

        await db.SaveChangesAsync(ct);

        var resumo = new ResumoEscrita(slug, dia, espinha.Count, encs, conds, naoMapeado, enfileirados);
        logger.LogInformation(
            "Klinikos escrita {Prov} {Dia}: {Enc} Encounter, {Cond} Condition ({NaoMap} CID sem código), "
            + "{Fila} enfileirados no deep.", slug, dia, encs, conds, naoMapeado, enfileirados);
        return resumo;
    }

    public async Task<IReadOnlyList<ResumoEscrita>> BackfillAsync(
        string slug, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var fonte = await resolver.ResolverAsync(slug, ct);
        GarantirPodeEscrever(slug, fonte.WebPrimaria);
        if (de > ate) throw new ValidacaoException("klinikos.backfill_intervalo", "Data inicial após a final.");

        var resultados = new List<ResumoEscrita>();
        for (var dia = de; dia <= ate; dia = dia.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            resultados.Add(await GravarEspinhaAsync(slug, dia, ct));
        }
        return resultados;
    }

    // ------------------------------------------------------------------ guardas e fila

    private void GarantirPodeEscrever(string slug, bool webPrimaria)
    {
        if (!opcoes.Value.EscritaHabilitada)
        {
            throw new ValidacaoException(
                "klinikos.escrita_desabilitada",
                "A escrita do conector web do Klinikos está desligada (KlinikosWeb:EscritaHabilitada=false).");
        }
        if (!webPrimaria)
        {
            throw new ValidacaoException(
                "klinikos.escrita_nao_primaria",
                $"A fonte '{slug}' não é primária web (webPrimaria=false) — a escrita web só é "
                + "permitida onde não há dono SQL (Conde).");
        }
    }

    /// <summary>Enfileira o boletim para o deep, se ainda não estiver na fila. Não salva (o chamador salva).</summary>
    private async Task<bool> EnfileirarDeepAsync(string slug, string spa, string unid, CancellationToken ct)
    {
        var jaExiste = await db.KlinikosDeepFilas
            .AnyAsync(x => x.Provedor == slug && x.SpaCodigo == spa, ct);
        if (jaExiste) return false;

        var agora = DateTime.UtcNow;
        db.KlinikosDeepFilas.Add(new KlinikosDeepFila
        {
            Id = Guid.NewGuid(),
            Provedor = slug,
            SpaCodigo = spa,
            UnidCodigo = unid,
            Prioridade = 100, // TODO(prioridade): internação/óbito/remoção antes de alta.
            Estado = KlinikosDeepEstado.Enfileirado,
            Tentativas = 0,
            CriadoEm = agora,
            AtualizadoEm = agora,
        });
        return true;
    }
}
