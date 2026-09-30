using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SMSMais.Core.Common.Tempo;

namespace SMSMais.Core.Integracoes.EsusSgWeb;

/// <summary>Uma linha da "Fila de Regulação" do ESUS SG (<c>controller-fila-*/buscar</c>).</summary>
public sealed record EsusSgLinhaFila(
    string IdEsusSg,
    string Recurso,
    string? CodigoInterno,
    string? Subprocedimentos,
    DateOnly? DataSolicitacao,
    DateOnly? DataEntradaFila,
    string? Prioridade,
    string? PrioridadeCor,
    string? Pendencia,
    int? PosicaoFila,
    int? OrdemEntrada,
    string? ProfissionalSolicitante,
    string? UnidadeSolicitante,
    string? UsuarioInclusao,
    string? Regulador,
    string? PessoaIdEsus,
    string PacienteNome,
    string? Cpf,
    string? Cns,
    DateOnly? DataNascimento,
    string? Sexo,
    string? NomeMae,
    string? Telefone,
    string? Celular,
    string? MunicipioPaciente,
    string? Bairro)
{
    /// <summary>Mapeia a linha crua. Nomes de campo medidos em 30/09/2026: <c>nome</c> é o
    /// PROCEDIMENTO (igual a <c>fle_nome_procedimento</c>); o paciente é <c>pes_nome</c>. CPF e CNS
    /// têm duas fontes (<c>pep_*</c> da pessoa e <c>pae_*</c> do paciente); a da pessoa é a que vem
    /// completa (CPF 517/655, CNS 652/655).</summary>
    public static EsusSgLinhaFila De(JsonElement r) => new(
        IdEsusSg: EsusSgJson.Texto(r, "fil_id") ?? throw new FormatException("linha da fila sem fil_id"),
        Recurso: EsusSgJson.Texto(r, "fle_nome_procedimento") ?? EsusSgJson.Texto(r, "nome") ?? "(sem procedimento)",
        CodigoInterno: EsusSgJson.Texto(r, "codigo_procedimento"),
        Subprocedimentos: EsusSgJson.Texto(r, "subprocedimentos"),
        DataSolicitacao: EsusSgJson.Data(r, "fil_data_pedido"),
        DataEntradaFila: EsusSgJson.Data(r, "fil_data"),
        Prioridade: EsusSgJson.Texto(r, "pfi_nome"),
        PrioridadeCor: EsusSgJson.Texto(r, "pfi_cor"),
        Pendencia: EsusSgJson.Texto(r, "pendencia"),
        PosicaoFila: EsusSgJson.Inteiro(r, "fil_ordem_regulada") ?? EsusSgJson.Inteiro(r, "ordem_regulada"),
        OrdemEntrada: EsusSgJson.Inteiro(r, "fil_ordem_entrada"),
        ProfissionalSolicitante: EsusSgJson.Texto(r, "profissional_solicitante"),
        UnidadeSolicitante: EsusSgJson.Texto(r, "uns_solicitante") ?? EsusSgJson.Texto(r, "unidade_fila"),
        UsuarioInclusao: EsusSgJson.Texto(r, "usu_nome"),
        Regulador: EsusSgJson.Texto(r, "nomeRegulador"),
        PessoaIdEsus: EsusSgJson.Texto(r, "pes_id"),
        PacienteNome: EsusSgJson.Texto(r, "pes_nome") ?? "(sem nome)",
        Cpf: EsusSgJson.Cpf(EsusSgJson.Texto(r, "pep_cpf_numero")) ?? EsusSgJson.Cpf(EsusSgJson.Texto(r, "pae_cpf")),
        Cns: EsusSgJson.Cns(EsusSgJson.Texto(r, "pep_cartaosus")) ?? EsusSgJson.Cns(EsusSgJson.Texto(r, "pae_cartao_sus")),
        DataNascimento: EsusSgJson.Data(r, "pep_nascimento"),
        Sexo: EsusSgJson.Texto(r, "pep_sexo"),
        NomeMae: EsusSgJson.Texto(r, "pep_mae"),
        Telefone: EsusSgJson.Texto(r, "telefone"),
        Celular: EsusSgJson.Texto(r, "nop_celular"),
        MunicipioPaciente: EsusSgJson.Texto(r, "mun_nome"),
        Bairro: EsusSgJson.Texto(r, "bai_nome"));
}

/// <summary>Uma linha de "Pacientes Agendados pela Fila" (<c>controller-paciente-agendado-*/buscar</c>).
/// <para><b>Uma linha é um AGENDAMENTO, não um pedido:</b> o mesmo <c>fil_id</c> pode ter vários
/// (<c>eap_id</c> e data diferentes — sessões de um tratamento). Medido em mar/2019: 100 linhas,
/// 94 pedidos, 97 agendamentos (3 linhas idênticas repetidas pelo próprio ESUS).</para></summary>
public sealed record EsusSgLinhaAgendado(
    string IdEsusSg,
    string? AgendamentoIdEsus,
    string Recurso,
    DateOnly? DataSolicitacao,
    DateOnly? DataEntradaFila,
    string? Prioridade,
    string? PrioridadeCor,
    string? ProfissionalSolicitante,
    string? UnidadeSolicitante,
    string? UsuarioInclusao,
    string? PessoaIdEsus,
    string PacienteNome,
    string? Cpf,
    string? Telefone,
    string? UnidadeExecutora,
    string? CnesExecutora,
    string? Setor,
    string? Local,
    DateOnly? DataAgendada,
    string? DataHoraAgendadaTexto,
    DateTime? DataHoraAgendada,
    string? UsuarioAgendamento,
    DateOnly? AgendamentoCadastradoEm,
    DateOnly? DataSaidaFila,
    bool? ComprovanteImpresso,
    bool? AgendadoTfd,
    string? NotificacaoTipo,
    string? NotificacaoEntrega,
    string? NotificacaoResposta)
{
    public static EsusSgLinhaAgendado De(JsonElement r)
    {
        var unidade = EsusSgJson.Texto(r, "unidadeAgendamento");
        var dataHoraTexto = EsusSgJson.Texto(r, "data_hora_formatada");
        return new(
            IdEsusSg: EsusSgJson.Texto(r, "fil_id") ?? throw new FormatException("agendado sem fil_id"),
            AgendamentoIdEsus: EsusSgJson.Texto(r, "eap_id"),
            Recurso: EsusSgJson.Texto(r, "stp_novo_nome_procedimento") ?? EsusSgJson.Texto(r, "set_nome") ?? "(sem procedimento)",
            DataSolicitacao: EsusSgJson.Data(r, "fil_data_pedido"),
            DataEntradaFila: EsusSgJson.Data(r, "fil_data"),
            Prioridade: EsusSgJson.Texto(r, "pfi_nome"),
            PrioridadeCor: EsusSgJson.Texto(r, "pfi_cor"),
            ProfissionalSolicitante: EsusSgJson.Texto(r, "nomeFuncionarioSolicitante"),
            UnidadeSolicitante: EsusSgJson.Texto(r, "unidadeFila") ?? EsusSgJson.Texto(r, "uns_solicitante"),
            UsuarioInclusao: EsusSgJson.Texto(r, "usuarioFila"),
            PessoaIdEsus: EsusSgJson.Texto(r, "pes_id"),
            PacienteNome: EsusSgJson.Texto(r, "nomePaciente") ?? "(sem nome)",
            Cpf: EsusSgJson.Cpf(EsusSgJson.Texto(r, "cpf_numero")),
            Telefone: EsusSgJson.Texto(r, "telefone"),
            UnidadeExecutora: unidade,
            CnesExecutora: EsusSgJson.CnesNoNome(unidade),
            Setor: EsusSgJson.Texto(r, "set_nome"),
            Local: EsusSgJson.Texto(r, "lca_nome"),
            DataAgendada: EsusSgJson.Data(r, "data_agendada") ?? EsusSgJson.Data(r, "eha_data_exame"),
            DataHoraAgendadaTexto: dataHoraTexto,
            DataHoraAgendada: EsusSgJson.DataHora(dataHoraTexto),
            UsuarioAgendamento: EsusSgJson.Texto(r, "usuarioAgendamento"),
            AgendamentoCadastradoEm: EsusSgJson.Data(r, "data_cadastro_agendamento"),
            DataSaidaFila: EsusSgJson.Data(r, "data_saida_fila"),
            ComprovanteImpresso: EsusSgJson.SimNao(EsusSgJson.Texto(r, "comprovanteImpresso")),
            AgendadoTfd: EsusSgJson.SimNao(EsusSgJson.Texto(r, "agendado_tfd")),
            NotificacaoTipo: EsusSgJson.Texto(r, "not_tipo"),
            NotificacaoEntrega: EsusSgJson.Texto(r, "not_delivered_status"),
            NotificacaoResposta: EsusSgJson.Texto(r, "not_resposta"));
    }
}

/// <summary>Um procedimento do combo "reguláveis por solicitante".</summary>
public sealed record EsusSgRecursoCatalogo(string Valor, string Rotulo);

/// <summary>Leitura tolerante do JSON do ESUS: o legado manda quase tudo como string, às vezes
/// número, às vezes vazio. Datas em <c>dd/mm/aaaa</c> (medido).</summary>
public static partial class EsusSgJson
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static string? Texto(JsonElement r, string chave)
    {
        if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty(chave, out var v)) return null;
        var s = v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
        s = s?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    public static int? Inteiro(JsonElement r, string chave) =>
        int.TryParse(Texto(r, chave), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    public static DateOnly? Data(JsonElement r, string chave) => Data(Texto(r, chave));

    /// <summary><c>dd/MM/yyyy</c> (com ou sem hora). O ESUS usa <c>31/12/1969</c> como "vazio" em
    /// alguns campos (epoch em UTC−3) — tratado como ausente.</summary>
    public static DateOnly? Data(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 10) return null;
        if (!DateOnly.TryParseExact(s[..10], "dd/MM/yyyy", Br, DateTimeStyles.None, out var d)
            && !DateOnly.TryParseExact(s[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
        {
            return null;
        }
        return d.Year < 1900 || (d.Year == 1969 && d.Month == 12 && d.Day == 31) ? null : d;
    }

    /// <summary><c>dd/MM/yyyy HH:mm:ss</c> em horário de Brasília → UTC.</summary>
    public static DateTime? DataHora(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (!DateTime.TryParseExact(s.Trim(), ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm"], Br,
                DateTimeStyles.None, out var local))
        {
            return null;
        }
        return FusoBrasilia.DeBrasiliaParaUtc(local);
    }

    public static string? Cpf(string? s)
    {
        var d = SoDigitos(s);
        return d is { Length: 11 } && d.Distinct().Count() > 1 ? d : null;
    }

    public static string? Cns(string? s)
    {
        var d = SoDigitos(s);
        return d is { Length: 15 } ? d : null;
    }

    public static bool? SimNao(string? s) => s?.ToUpperInvariant() switch
    {
        "SIM" or "S" or "1" or "TRUE" => true,
        "NÃO" or "NAO" or "N" or "0" or "FALSE" => false,
        _ => null,
    };

    /// <summary>"ABRAE  2297523" / "OFTALMOCLÍNICA SÃO GONÇALO - 2291525" → "2297523". O CNES tem
    /// 7 dígitos e vem no fim do nome; nome sem CNES (ex.: "VISATTO UNIDADE RODOSHOPPING") → null.</summary>
    public static string? CnesNoNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return null;
        var m = RegexCnesNoFim().Match(nome);
        return m.Success && m.Groups[1].Value != "0000001" ? m.Groups[1].Value : null;
    }

    private static string? SoDigitos(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : new string(s.Where(char.IsDigit).ToArray());

    [GeneratedRegex(@"(?:^|[\s\-])(\d{7})\s*$")]
    private static partial Regex RegexCnesNoFim();
}
