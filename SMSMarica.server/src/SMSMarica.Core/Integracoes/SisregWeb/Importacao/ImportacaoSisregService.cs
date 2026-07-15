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
/// Importação de agendamentos do SISREG para <c>SolicitacaoExame</c>, a partir do export de
/// agendamentos (<c>expo_solicitacoes</c>) — aceita TXT (com cabeçalho de unidade) ou CSV (com
/// cabeçalho de colunas). Ver <see cref="AgendaTxtParser"/>.
///  - PREVIEW: só leitura, monta o "diff" (novo vs já existe).
///  - EXECUTAR: roda o fluxo inteiro de UMA marcação (o operador importa 1 a 1 e confere).
/// </summary>
public interface IImportacaoSisregService
{
    /// <param name="nomeArquivo">Nome do arquivo enviado (usado no CSV para achar o executante).</param>
    Task<ImportacaoPreviewResultado> PreviewDeTextoAsync(string conteudo, string? nomeArquivo, CancellationToken ct);

    /// <summary>Importa UMA marcação (por código) do arquivo enviado. Cria paciente/unidades/solicitação.</summary>
    Task<ImportacaoExecucaoResultado> ExecutarUmAsync(string conteudo, string codigoSolicitacao, string? nomeArquivo, CancellationToken ct);
}

public sealed class ImportacaoSisregService(
    SmsMaricaDbContext db,
    IConsultaCnsService consultaCns,
    IPacientesService pacientes,
    IGeradorIdentificadores geradorIds,
    IUsuarioAtualAccessor usuarioAtual,
    Notificacoes.Comunicacao.IComunicacaoPacienteService comunicacoes) : IImportacaoSisregService
{
    // ===================== PREVIEW =====================

    public async Task<ImportacaoPreviewResultado> PreviewDeTextoAsync(string conteudo, string? nomeArquivo, CancellationToken ct)
    {
        var parsed = ParseOuFalhar(conteudo, nomeArquivo);
        var inicio = parsed.Cabecalho.Inicio ?? parsed.Marcacoes.Min(m => m.DataHoraAtendimento)?.ToDateOnly() ?? default;
        var fim = parsed.Cabecalho.Fim ?? parsed.Marcacoes.Max(m => m.DataHoraAtendimento)?.ToDateOnly() ?? default;
        // A unidade EXECUTORA é resolvida uma vez para o arquivo inteiro (é o tenant atual).
        var executante = await ResolverExecutanteAsync(parsed.Cabecalho.CnesUnidade, parsed.Cabecalho.NomeUnidade, ct);
        return await MontarPreviewAsync(parsed.Marcacoes, inicio, fim, executante, ct);
    }

    private async Task<ImportacaoPreviewResultado> MontarPreviewAsync(
        IReadOnlyList<MarcacaoSisreg> marcacoes, DateOnly inicio, DateOnly fim, ExecutanteResolvido executante, CancellationToken ct)
    {
        var codigos = marcacoes.Select(m => m.CodigoSolicitacao).Distinct().ToList();
        var existentes = (await db.Solicitacoes.AsNoTracking()
            .Where(s => s.ExcluidoEm == null && s.CodigoSolicitacao != null && codigos.Contains(s.CodigoSolicitacao))
            .Select(s => s.CodigoSolicitacao!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        // Existência da SOLICITANTE por CNES (o arquivo sempre traz o CNES do solicitante).
        var cnesSolic = marcacoes
            .Select(m => m.CnesUnidadeSolicitante)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        var unidadesComCnes = (await db.Unidades.AsNoTracking()
            .Where(u => u.Cnes != null && cnesSolic.Contains(u.Cnes))
            .Select(u => u.Cnes!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var sigtapComTipo = await SigtapComTipoAsync(ct);

        // Executante já resolvida (tenant atual); o mesmo nome/CNES vale para todas as linhas.
        var execNome = executante.Nome ?? marcacoes.Select(m => m.NomeUnidadeExecutante).FirstOrDefault(n => n is not null);
        var execExiste = executante.Id is not null;

        var itens = new List<ImportacaoPreviewItem>(marcacoes.Count);
        foreach (var m in marcacoes)
        {
            var categoria = CategoriaPorSigtap(m.CodigoSigtap);
            // "Mapeia" só faz sentido para imagem (é quem vira satélite/worklist).
            var procMapeia = categoria == CategoriaSolicitacao.Imagem
                && m.CodigoSigtap is { } sig && sigtapComTipo.Contains(sig);
            var alertas = new List<string>();
            if (executante.Erro is { } erroExec) alertas.Add(erroExec);
            if (categoria == CategoriaSolicitacao.Imagem && !procMapeia)
                alertas.Add("Exame de imagem sem tipo mapeado — importa como pendente (mapear depois).");
            else if (categoria != CategoriaSolicitacao.Imagem)
                alertas.Add($"Categoria {categoria} — importa como solicitação (sem exame de imagem/PACS).");
            if (string.IsNullOrWhiteSpace(m.CnsPaciente)) alertas.Add("Sem CNS do paciente.");
            if (m.DataHoraAtendimento is null) alertas.Add("Sem data/hora de atendimento.");

            itens.Add(new ImportacaoPreviewItem(
                m.CodigoSolicitacao, m.NomePaciente, m.CnsPaciente, m.ProcedimentoTexto, m.DataHoraAtendimento,
                m.NomeUnidadeSolicitante, m.CnesUnidadeSolicitante, execNome, executante.Cnes,
                m.NomeMedicoSolicitante,
                JaExiste: existentes.Contains(m.CodigoSolicitacao),
                UnidadeSolicitanteExiste: m.CnesUnidadeSolicitante is { } cs && unidadesComCnes.Contains(cs),
                UnidadeExecutanteExiste: execExiste,
                ProcedimentoMapeia: procMapeia,
                Alertas: alertas));
        }

        itens = [.. itens.OrderBy(i => i.JaExiste).ThenBy(i => i.DataHoraAtendimento)];
        var novos = itens.Count(i => !i.JaExiste);
        return new ImportacaoPreviewResultado(inicio, fim, itens.Count, novos, itens.Count - novos, itens);
    }

    // ===================== EXECUTAR (1 registro) =====================

    public async Task<ImportacaoExecucaoResultado> ExecutarUmAsync(string conteudo, string codigoSolicitacao, string? nomeArquivo, CancellationToken ct)
    {
        var codigo = (codigoSolicitacao ?? string.Empty).Trim();
        var m = ParseOuFalhar(conteudo, nomeArquivo).Marcacoes.FirstOrDefault(x => x.CodigoSolicitacao == codigo)
            ?? throw new NaoEncontradoException("importacao.marcacao", codigo);

        var passos = new List<string>();
        ImportacaoExecucaoResultado Falha(string erro) => new(codigo, false, null, null, null, false, false, false, passos, erro);

        // 1. Idempotência (nº SISREG).
        if (await db.Solicitacoes.AsNoTracking().AnyAsync(
                s => s.CodigoSolicitacao == codigo && s.ExcluidoEm == null, ct))
            return Falha("Já existe uma solicitação com esse número do SISREG.");

        // 2. Natureza pelo subgrupo SIGTAP (roteia satélite/UI). SÓ imagem precisa de TipoExame —
        //    e mesmo SEM tipo mapeado a importação NÃO trava: entra como pendente (ADR-0021).
        var sig = SoDigitos(m.CodigoSigtap);
        var categoria = CategoriaPorSigtap(sig);
        Guid? tipoExameId = null;
        if (categoria == CategoriaSolicitacao.Imagem)
        {
            var tipos = await db.TiposExame.AsNoTracking()
                .Where(t => t.ExcluidoEm == null && t.Ativo && t.ProcedimentoSigtap != null)
                .Select(t => new { t.Id, Codigo = t.ProcedimentoSigtap!.Codigo })
                .ToListAsync(ct);
            tipoExameId = tipos.FirstOrDefault(t => SoDigitos(t.Codigo) == sig)?.Id;
            passos.Add(tipoExameId is null
                ? $"Exame de imagem \"{m.ProcedimentoTexto}\" (SIGTAP {sig}) — SEM tipo mapeado; importa como PENDENTE."
                : $"Exame de imagem \"{m.ProcedimentoTexto}\" → tipo mapeado (SIGTAP {sig}).");
        }
        else
        {
            passos.Add($"Categoria {categoria} (SIGTAP {sig}) — importa como solicitação, sem satélite de execução.");
        }

        // 3. Paciente: CNS → cadweb50 (CPF+demografia) → resolve por CPF → cria se não existir.
        if (string.IsNullOrWhiteSpace(m.CnsPaciente)) return Falha("Marcação sem CNS do paciente.");
        ConsultaCnsRespostaDto cadsus;
        try { cadsus = await consultaCns.ConsultarPorCnsAsync(m.CnsPaciente!, ct); }
        catch (Exception ex) { return Falha($"Falha ao consultar o paciente no SISREG (CNS): {ex.Message}"); }
        passos.Add($"CNS {Mascara(m.CnsPaciente)} → CPF {Mascara(cadsus.Cpf)} (cadweb50).");

        // Resolve o paciente existente por CPF (do CADSUS) e, se faltar, por CNS — o cidadão
        // pode já estar no hub sob o CNS mesmo quando o CADSUS não devolve o CPF. Nunca altera o nome.
        var existente = await pacientes.ObterPorCpfAsync(cadsus.Cpf, ct)
                        ?? await pacientes.ObterPorCnsAsync(m.CnsPaciente!, ct);
        Guid pacienteId;
        bool pacienteCriado;
        if (existente is not null)
        {
            pacienteId = existente.Id;
            pacienteCriado = false;
            passos.Add("Paciente já cadastrado — reusa, sem alterar o nome.");
        }
        else
        {
            // Paciente inexistente E sem CPF do CADSUS: não dá para cadastrar com segurança
            // (sem CPF não há identidade). Cai em falha honesta em vez do falso "CPF duplicado".
            if (SoDigitos(cadsus.Cpf).Length != 11)
                return Falha("O CADSUS não retornou o CPF deste CNS e o paciente ainda não existe no sistema. Cadastre o paciente manualmente e reimporte.");

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

        // 4. Unidades. EXECUTORA = o tenant atual (contexto da unidade em que o operador importa) —
        //    atribuição explícita, resolvida uma vez para o arquivo. SOLICITANTE = por CNES do
        //    arquivo (cria se ainda não existir).
        var exec = await ResolverExecutanteAsync(m.CnesUnidadeExecutante, m.NomeUnidadeExecutante, ct);
        if (exec.Id is null) return Falha(exec.Erro ?? "Não identifiquei a unidade executante.");
        var unidadeExecId = exec.Id.Value;
        var execCriada = exec.Criada;
        passos.Add(execCriada
            ? $"Unidade executante criada do cabeçalho: {exec.Nome}{CnesSufixo(exec.Cnes)}."
            : $"Unidade executante: {exec.Nome} (contexto atual).");

        var (unidadeSolicId, solicCriada) = await ResolverOuCriarUnidadeAsync(m.CnesUnidadeSolicitante, m.NomeUnidadeSolicitante, ct);
        if (solicCriada) passos.Add($"Unidade solicitante criada: {m.NomeUnidadeSolicitante}{CnesSufixo(m.CnesUnidadeSolicitante)}.");
        else if (unidadeSolicId is not null) passos.Add("Unidade solicitante já cadastrada.");

        // 5. Espinha de regulação (sempre) + satélite de execução SÓ para imagem. Ver ADR-0021.
        var agora = DateTime.UtcNow;
        var solic = new Solicitacao
        {
            Id = Guid.CreateVersion7(),
            PacienteId = pacienteId,
            Categoria = categoria,
            ProcedimentoSigtapCodigo = m.CodigoSigtap,
            ProcedimentoTexto = m.ProcedimentoTexto,
            // Consulta colapsa no SIGTAP 0301010072 — a especialidade só existe no texto.
            EspecialidadeTexto = categoria == CategoriaSolicitacao.Consulta ? m.ProcedimentoTexto : null,
            UnidadeExecutanteId = unidadeExecId,
            UnidadeSolicitanteId = unidadeSolicId,
            SolicitanteNome = m.NomeMedicoSolicitante ?? "NÃO INFORMADO",
            SolicitanteNumConselho = string.Empty,
            SolicitanteUfConselho = string.Empty,
            SolicitanteConselho = "CRM",
            SolicitanteCpf = m.CpfMedicoSolicitante,
            RawSisreg = m.LinhaRaw,
            CodigoSolicitacao = codigo,
            Status = StatusSolicitacao.Solicitada,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            // O SISREG entrega hora LOCAL de Brasília (GMT-3) → UTC (+3h).
            DataAgendada = m.DataHoraAtendimento is { } dh ? ParaUtcBrasilia(dh) : null,
            DataSolicitacao = m.DataSolicitacao,
            DataRegulacao = m.DataRegulacao,
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.Solicitacoes.Add(solic);

        // AccessionNumber/StudyUID são conceitos DICOM — só existem no satélite de imagem.
        Guid idPublico = solic.Id;
        string? accession = null;
        if (categoria == CategoriaSolicitacao.Imagem)
        {
            accession = await geradorIds.ProximoAccessionAsync(ct);
            var exame = new ExameImagem
            {
                Id = Guid.CreateVersion7(),
                Solicitacao = solic,
                AccessionNumber = accession,
                StudyInstanceUID = geradorIds.NovoStudyInstanceUid(),
                TipoExameId = tipoExameId, // nullable = mapeamento pendente
                Status = StatusSolicitacaoExame.Solicitada,
                // NADA vai ao PACS automaticamente: só quando a recepção AUTORIZA.
                ProximaTentativaEm = null,
                CriadoEm = agora,
                CriadoPor = usuarioAtual.UsuarioId,
            };
            db.ExamesImagem.Add(exame);
            idPublico = exame.Id;
        }

        // Notificação WhatsApp de confirmação — só enfileira (o worker envia com ritmo).
        await comunicacoes.EnfileirarAsync(solic, Data.Entities.Enums.FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
        await db.SaveChangesAsync(ct);
        passos.Add(accession is null ? "Solicitação criada." : $"Solicitação criada (accession {accession}).");

        return new ImportacaoExecucaoResultado(
            codigo, true, idPublico, accession ?? string.Empty,
            existente?.NomeCompleto ?? cadsus.Nome, pacienteCriado, solicCriada, execCriada, passos, null);
    }

    // ===================== helpers =====================

    private static AgendaTxtParser.Resultado ParseOuFalhar(string conteudo, string? nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new ValidacaoException("importacao.arquivo_vazio", "Arquivo vazio ou ilegível.");
        var parsed = AgendaTxtParser.Parse(conteudo, nomeArquivo);
        if (parsed.Marcacoes.Count == 0)
            throw new ValidacaoException("importacao.sem_registros",
                "Não encontrei marcações no arquivo. Confirme que é o export de agendamentos do SISREG (TXT ou CSV).");
        return parsed;
    }

    private async Task<HashSet<string>> SigtapComTipoAsync(CancellationToken ct) =>
        (await db.TiposExame.AsNoTracking()
            .Where(t => t.ExcluidoEm == null && t.Ativo && t.ProcedimentoSigtap != null)
            .Select(t => t.ProcedimentoSigtap!.Codigo)
            .ToListAsync(ct))
        .Select(SoDigitos).Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);

    /// <summary>Unidade executante resolvida para o arquivo (uma só). <see cref="Erro"/> não-nulo
    /// quando não foi possível determinar (aí a importação não prossegue).</summary>
    private sealed record ExecutanteResolvido(Guid? Id, string? Nome, string? Cnes, bool Criada, string? Erro);

    /// <summary>
    /// Resolve a unidade EXECUTORA do arquivo. Prioridade:
    ///  1. <b>Tenant atual</b> (header <c>X-Unidade-Id</c>) — o contexto de unidade em que o
    ///     operador está importando. É a atribuição explícita e vale para TXT e CSV.
    ///  2. <b>CNES do cabeçalho do arquivo</b> (só o TXT traz) — fallback quando não há tenant
    ///     (ex.: admin global sem unidade ativa); resolve/cria por CNES.
    ///  3. Sem tenant e sem CNES no arquivo (CSV fora de contexto) → erro pedindo para o operador
    ///     entrar no contexto da unidade.
    /// </summary>
    private async Task<ExecutanteResolvido> ResolverExecutanteAsync(string? cnesArquivo, string? nomeExecArquivo, CancellationToken ct)
    {
        if (usuarioAtual.UnidadeAtivaId is { } uid)
        {
            var u = await db.Unidades.AsNoTracking()
                .Where(x => x.Id == uid && x.Ativo)
                .Select(x => new { x.Id, x.Nome, x.Cnes })
                .FirstOrDefaultAsync(ct);
            if (u is not null) return new(u.Id, u.Nome, u.Cnes, false, null);
            // Header presente mas unidade inexistente/inativa: cai no fallback do arquivo.
        }

        if (SoDigitos(cnesArquivo) is { Length: 7 } c)
        {
            var (id, criada) = await ResolverOuCriarUnidadeAsync(c, nomeExecArquivo, ct);
            if (id is not null) return new(id.Value, nomeExecArquivo, c, criada, null);
        }

        return new(null, null, null, false,
            "Selecione a unidade executante (entre no contexto da unidade) antes de importar — o arquivo não traz o CNES do executante.");
    }

    /// <summary>Resolve unidade por CNES; cria (nome UPPERCASE) se não existir. Retorna (id, criada).</summary>
    private async Task<(Guid? id, bool criada)> ResolverOuCriarUnidadeAsync(string? cnes, string? nome, CancellationToken ct)
    {
        var cnesLimpo = SoDigitos(cnes);
        if (cnesLimpo.Length != 7) return (null, false);

        var existente = await db.Unidades.Where(u => u.Cnes == cnesLimpo).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (existente is not null) return (existente, false);

        var u = new Unidade
        {
            Id = Guid.CreateVersion7(),
            Nome = (string.IsNullOrWhiteSpace(nome) ? $"UNIDADE CNES {cnesLimpo}" : nome).Trim().ToUpperInvariant(),
            Cnes = cnesLimpo,
            Ativo = true,
            Externa = false,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(u);
        await db.SaveChangesAsync(ct);
        return (u.Id, true);
    }

    private static string CnesSufixo(string? cnes) =>
        SoDigitos(cnes) is { Length: 7 } c ? $" (CNES {c})" : " (sem CNES)";

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

    /// <summary>Fuso de Brasília (GMT-3, sem horário de verão desde 2019 — datas de 2026 não têm DST).</summary>
    private static readonly TimeSpan OffsetBrasilia = TimeSpan.FromHours(-3);

    /// <summary>Interpreta a data/hora como wall-clock de Brasília (-03:00) e devolve o instante em UTC.</summary>
    private static DateTime ParaUtcBrasilia(DateTime wallClock) =>
        new DateTimeOffset(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified), OffsetBrasilia).UtcDateTime;

    private static string SoDigitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);

    /// <summary>Categoria (natureza clínica) pelo SUBGRUPO SIGTAP (4 primeiros dígitos) — determinístico,
    /// não adivinha procedimento. Ver ADR-0021. Só <see cref="CategoriaSolicitacao.Imagem"/> cria satélite.</summary>
    private static CategoriaSolicitacao CategoriaPorSigtap(string? sigtap)
    {
        var d = SoDigitos(sigtap);
        if (d.Length < 4) return CategoriaSolicitacao.Outro;
        return d[..4] switch
        {
            "0301" or "0302" => CategoriaSolicitacao.Consulta,
            "0204" or "0205" or "0206" => CategoriaSolicitacao.Imagem, // RX/mamo/densito · US · TC/RM
            "0202" or "0203" => CategoriaSolicitacao.Laboratorio,      // lab clínico · patologia
            "0211" => CategoriaSolicitacao.GraficoFuncional,          // ECG, EEG, audiometria, espirometria...
            "0209" => CategoriaSolicitacao.Endoscopia,
            _ => d[..2] == "04" ? CategoriaSolicitacao.Cirurgia : CategoriaSolicitacao.Outro,
        };
    }

    private static string Mascara(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : v.Length <= 4 ? "***" : v[..3] + "***" + v[^2..];
}

internal static class DataHoraExtensions
{
    public static DateOnly ToDateOnly(this DateTime dt) => DateOnly.FromDateTime(dt);
}
