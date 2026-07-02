using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Importação de agendamentos do SISREG para <c>SolicitacaoExame</c>, a partir do
/// "Arquivo Agendamento (TXT)" (<c>expo_solicitacoes</c>).
///  - PREVIEW: só leitura, monta o "diff" (novo vs já existe).
///  - EXECUTAR: roda o fluxo inteiro de UMA marcação (o operador importa 1 a 1 e confere).
/// </summary>
public interface IImportacaoSisregService
{
    Task<ImportacaoPreviewResultado> PreviewDeTextoAsync(string conteudoTxt, CancellationToken ct);

    /// <summary>Importa UMA marcação (por código) do TXT enviado. Cria paciente/unidades/solicitação.</summary>
    Task<ImportacaoExecucaoResultado> ExecutarUmAsync(string conteudoTxt, string codigoSolicitacao, CancellationToken ct);
}

public sealed class ImportacaoSisregService(
    SmsMaricaDbContext db,
    IConsultaCnsService consultaCns,
    IPacientesService pacientes,
    IGeradorIdentificadores geradorIds,
    IUsuarioAtualAccessor usuarioAtual) : IImportacaoSisregService
{
    // ===================== PREVIEW =====================

    public async Task<ImportacaoPreviewResultado> PreviewDeTextoAsync(string conteudoTxt, CancellationToken ct)
    {
        var parsed = ParseOuFalhar(conteudoTxt);
        var inicio = parsed.Cabecalho.Inicio ?? parsed.Marcacoes.Min(m => m.DataHoraAtendimento)?.ToDateOnly() ?? default;
        var fim = parsed.Cabecalho.Fim ?? parsed.Marcacoes.Max(m => m.DataHoraAtendimento)?.ToDateOnly() ?? default;
        return await MontarPreviewAsync(parsed.Marcacoes, inicio, fim, ct);
    }

    private async Task<ImportacaoPreviewResultado> MontarPreviewAsync(
        IReadOnlyList<MarcacaoSisreg> marcacoes, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        var codigos = marcacoes.Select(m => m.CodigoSolicitacao).Distinct().ToList();
        var existentes = (await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.ExcluidoEm == null && s.CodigoSolicitacao != null && codigos.Contains(s.CodigoSolicitacao))
            .Select(s => s.CodigoSolicitacao!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var cnesSet = marcacoes
            .SelectMany(m => new[] { m.CnesUnidadeSolicitante, m.CnesUnidadeExecutante })
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        var unidadesComCnes = (await db.Unidades.AsNoTracking()
            .Where(u => u.Cnes != null && cnesSet.Contains(u.Cnes))
            .Select(u => u.Cnes!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var sigtapComTipo = await SigtapComTipoAsync(ct);

        var itens = new List<ImportacaoPreviewItem>(marcacoes.Count);
        foreach (var m in marcacoes)
        {
            var procMapeia = m.CodigoSigtap is { } sig && sigtapComTipo.Contains(sig);
            var alertas = new List<string>();
            if (!procMapeia) alertas.Add("Procedimento/SIGTAP sem tipo de exame mapeado (cairia em divergência).");
            if (string.IsNullOrWhiteSpace(m.CnsPaciente)) alertas.Add("Sem CNS do paciente.");
            if (m.DataHoraAtendimento is null) alertas.Add("Sem data/hora de atendimento.");

            itens.Add(new ImportacaoPreviewItem(
                m.CodigoSolicitacao, m.NomePaciente, m.CnsPaciente, m.ProcedimentoTexto, m.DataHoraAtendimento,
                m.NomeUnidadeSolicitante, m.CnesUnidadeSolicitante, m.NomeUnidadeExecutante, m.CnesUnidadeExecutante,
                m.NomeMedicoSolicitante,
                JaExiste: existentes.Contains(m.CodigoSolicitacao),
                UnidadeSolicitanteExiste: m.CnesUnidadeSolicitante is { } cs && unidadesComCnes.Contains(cs),
                UnidadeExecutanteExiste: m.CnesUnidadeExecutante is { } ce && unidadesComCnes.Contains(ce),
                ProcedimentoMapeia: procMapeia,
                Alertas: alertas));
        }

        itens = [.. itens.OrderBy(i => i.JaExiste).ThenBy(i => i.DataHoraAtendimento)];
        var novos = itens.Count(i => !i.JaExiste);
        return new ImportacaoPreviewResultado(inicio, fim, itens.Count, novos, itens.Count - novos, itens);
    }

    // ===================== EXECUTAR (1 registro) =====================

    public async Task<ImportacaoExecucaoResultado> ExecutarUmAsync(string conteudoTxt, string codigoSolicitacao, CancellationToken ct)
    {
        var codigo = (codigoSolicitacao ?? string.Empty).Trim();
        var m = ParseOuFalhar(conteudoTxt).Marcacoes.FirstOrDefault(x => x.CodigoSolicitacao == codigo)
            ?? throw new NaoEncontradoException("importacao.marcacao", codigo);

        var passos = new List<string>();
        ImportacaoExecucaoResultado Falha(string erro) => new(codigo, false, null, null, null, false, false, passos, erro);

        // 1. Idempotência (nº SISREG).
        if (await db.SolicitacoesExame.AsNoTracking().AnyAsync(
                s => s.CodigoSolicitacao == codigo && s.ExcluidoEm == null, ct))
            return Falha("Já existe uma solicitação com esse número do SISREG.");

        // 2. Procedimento → tipo de exame (por SIGTAP).
        var sig = m.CodigoSigtap ?? string.Empty;
        var tipoExame = await db.TiposExame.AsNoTracking()
            .Where(t => t.ExcluidoEm == null && t.Ativo && t.ProcedimentoSigtap != null)
            .Select(t => new { t.Id, t.EnviarParaWorklist, Codigo = t.ProcedimentoSigtap!.Codigo })
            .ToListAsync(ct);
        var tipo = tipoExame.FirstOrDefault(t => SoDigitos(t.Codigo) == sig);
        if (tipo is null) return Falha("Procedimento (SIGTAP) sem tipo de exame mapeado — cai em divergência.");
        passos.Add($"Procedimento {m.ProcedimentoTexto} → tipo de exame (SIGTAP {sig}).");

        // 3. Paciente: CNS → cadweb50 (CPF+demografia) → resolve por CPF → cria se não existir.
        if (string.IsNullOrWhiteSpace(m.CnsPaciente)) return Falha("Marcação sem CNS do paciente.");
        ConsultaCnsRespostaDto cadsus;
        try { cadsus = await consultaCns.ConsultarPorCnsAsync(m.CnsPaciente!, ct); }
        catch (Exception ex) { return Falha($"Falha ao consultar o paciente no SISREG (CNS): {ex.Message}"); }
        passos.Add($"CNS {Mascara(m.CnsPaciente)} → CPF {Mascara(cadsus.Cpf)} (cadweb50).");

        var existente = await pacientes.ObterPorCpfAsync(cadsus.Cpf, ct);
        Guid pacienteId;
        bool pacienteCriado;
        if (existente is not null)
        {
            pacienteId = existente.Id;
            pacienteCriado = false;
            passos.Add($"Paciente já cadastrado (CPF) — reusa, sem alterar o nome.");
        }
        else
        {
            // Telefone do TXT vai num slot NÃO-principal (celular se móvel, senão residencial) —
            // o principal é o contato validado por OTP e é intocável pela automação (ADR-0020).
            var (celular, residencial) = MontarTelefoneDoTxt(m.TelefonePaciente);
            // Endereço só entra no CREATE (paciente novo); paciente existente nunca é sobrescrito.
            var endereco = MontarEnderecoDoTxt(m);

            pacienteId = await pacientes.CadastrarAsync(new CadastrarPacienteRequest(
                NomeCompleto: (cadsus.Nome.Length > 0 ? cadsus.Nome : m.NomePaciente) ?? "SEM NOME",
                Cpf: cadsus.Cpf,
                DataNascimento: cadsus.DataNascimento ?? default,
                Cns: cadsus.Cns,
                Rg: null,
                Sexo: cadsus.Sexo == "Masculino" ? Sexo.Masculino : cadsus.Sexo == "Feminino" ? Sexo.Feminino : Sexo.NaoInformado,
                NomeDaMae: cadsus.NomeMae,
                Endereco: endereco,
                TelefoneCelular: celular,
                TelefoneResidencial: residencial), ct);
            pacienteCriado = true;
            passos.Add("Paciente novo → criado a partir do CADSUS (+ telefone/endereço do TXT).");
        }

        // 4. Unidades (por CNES; cria a solicitante se faltar).
        var (unidadeExecId, _) = await ResolverOuCriarUnidadeAsync(m.CnesUnidadeExecutante, m.NomeUnidadeExecutante, ct);
        if (unidadeExecId is null) return Falha("Sem unidade executante (CNES) na marcação.");
        var (unidadeSolicId, solicCriada) = await ResolverOuCriarUnidadeAsync(m.CnesUnidadeSolicitante, m.NomeUnidadeSolicitante, ct);
        if (solicCriada) passos.Add($"Unidade solicitante criada: {m.NomeUnidadeSolicitante} (CNES {m.CnesUnidadeSolicitante}).");
        else if (unidadeSolicId is not null) passos.Add("Unidade solicitante já cadastrada.");

        // 5. Solicitação (médico do SISREG = texto; CRM vazio, CPF interno; data do SISREG).
        var accession = await geradorIds.ProximoAccessionAsync(ct);
        var agora = DateTime.UtcNow;
        var solic = new SolicitacaoExame
        {
            Id = Guid.CreateVersion7(),
            AccessionNumber = accession,
            StudyInstanceUID = geradorIds.NovoStudyInstanceUid(),
            PacienteId = pacienteId,
            TipoExameId = tipo.Id,
            UnidadeId = unidadeExecId.Value,
            UnidadeSolicitanteId = unidadeSolicId,
            SolicitanteNome = m.NomeMedicoSolicitante ?? "NÃO INFORMADO",
            SolicitanteNumConselho = string.Empty,
            SolicitanteUfConselho = string.Empty,
            SolicitanteConselho = "CRM",
            SolicitanteCpf = m.CpfMedicoSolicitante,
            CodigoSolicitacao = codigo,
            Status = StatusSolicitacaoExame.Solicitada,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = m.DataHoraAtendimento is { } dh
                ? DateTime.SpecifyKind(dh, DateTimeKind.Local).ToUniversalTime()
                : null,
            // Segue a config do tipo (worklist ligado → worker envia; senão só registra).
            ProximaTentativaEm = tipo.EnviarParaWorklist ? agora : null,
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.SolicitacoesExame.Add(solic);
        await db.SaveChangesAsync(ct);
        passos.Add($"Solicitação criada (accession {accession}).");

        return new ImportacaoExecucaoResultado(
            codigo, true, solic.Id, accession,
            existente?.NomeCompleto ?? cadsus.Nome, pacienteCriado, solicCriada, passos, null);
    }

    // ===================== helpers =====================

    private static AgendaTxtParser.Resultado ParseOuFalhar(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new ValidacaoException("importacao.arquivo_vazio", "Arquivo vazio ou ilegível.");
        var parsed = AgendaTxtParser.Parse(conteudo);
        if (parsed.Marcacoes.Count == 0)
            throw new ValidacaoException("importacao.sem_registros",
                "Não encontrei marcações no arquivo. Confirme que é o TXT de Arquivo Agendamento do SISREG.");
        return parsed;
    }

    private async Task<HashSet<string>> SigtapComTipoAsync(CancellationToken ct) =>
        (await db.TiposExame.AsNoTracking()
            .Where(t => t.ExcluidoEm == null && t.Ativo && t.ProcedimentoSigtap != null)
            .Select(t => t.ProcedimentoSigtap!.Codigo)
            .ToListAsync(ct))
        .Select(SoDigitos).Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);

    /// <summary>Resolve unidade por CNES; cria (nome UPPERCASE) se não existir. Retorna (id, criada).</summary>
    private async Task<(Guid? id, bool criada)> ResolverOuCriarUnidadeAsync(string? cnes, string? nome, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cnes)) return (null, false);
        var existente = await db.Unidades.Where(u => u.Cnes == cnes).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (existente is not null) return (existente, false);

        var u = new Unidade
        {
            Id = Guid.CreateVersion7(),
            Nome = (string.IsNullOrWhiteSpace(nome) ? $"UNIDADE CNES {cnes}" : nome).Trim().ToUpperInvariant(),
            Cnes = cnes,
            Ativo = true,
            Externa = false,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(u);
        await db.SaveChangesAsync(ct);
        return (u.Id, true);
    }

    /// <summary>Telefone do TXT em slot NÃO-principal: celular se for móvel (11 díg. e 3º = '9'),
    /// senão residencial. Nunca o Principal (esse é o contato validado por OTP).</summary>
    private static (string? celular, string? residencial) MontarTelefoneDoTxt(string? telefone)
    {
        var d = SoDigitos(telefone);
        if (d.Length is < 10 or > 11) return (null, null);
        var movel = d.Length == 11 && d[2] == '9';
        return movel ? (d, null) : (null, d);
    }

    /// <summary>Monta o EnderecoDto a partir das colunas do TXT. Null se não houver nada útil.</summary>
    private static EnderecoDto? MontarEnderecoDoTxt(MarcacaoSisreg m)
    {
        var logradouro = string.Join(' ', new[] { m.TipoLogradouro, m.Logradouro }
            .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        var temAlgo = !string.IsNullOrWhiteSpace(logradouro) || !string.IsNullOrWhiteSpace(m.Cep)
            || !string.IsNullOrWhiteSpace(m.Bairro) || !string.IsNullOrWhiteSpace(m.MunicipioResidencia);
        if (!temAlgo) return null;

        return new EnderecoDto(
            Cep: SoDigitos(m.Cep) is { Length: 8 } cep ? cep : string.Empty,
            Logradouro: logradouro,
            Numero: string.IsNullOrWhiteSpace(m.Numero) ? null : m.Numero,
            Complemento: m.Complemento,
            Bairro: m.Bairro ?? string.Empty,
            Cidade: m.MunicipioResidencia ?? string.Empty,
            Uf: UfDoIbge(m.CodigoIbgeResidencia),
            PontoReferencia: null);
    }

    /// <summary>UF a partir dos 2 primeiros dígitos do código IBGE do município (33→RJ, etc.).</summary>
    private static string UfDoIbge(string? ibge)
    {
        var d = SoDigitos(ibge);
        if (d.Length < 2) return string.Empty;
        return d[..2] switch
        {
            "11" => "RO", "12" => "AC", "13" => "AM", "14" => "RR", "15" => "PA", "16" => "AP", "17" => "TO",
            "21" => "MA", "22" => "PI", "23" => "CE", "24" => "RN", "25" => "PB", "26" => "PE", "27" => "AL", "28" => "SE", "29" => "BA",
            "31" => "MG", "32" => "ES", "33" => "RJ", "35" => "SP",
            "41" => "PR", "42" => "SC", "43" => "RS",
            "50" => "MS", "51" => "MT", "52" => "GO", "53" => "DF",
            _ => string.Empty,
        };
    }

    private static string SoDigitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);

    private static string Mascara(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : v.Length <= 4 ? "***" : v[..3] + "***" + v[^2..];
}

internal static class DataHoraExtensions
{
    public static DateOnly ToDateOnly(this DateTime dt) => DateOnly.FromDateTime(dt);
}
