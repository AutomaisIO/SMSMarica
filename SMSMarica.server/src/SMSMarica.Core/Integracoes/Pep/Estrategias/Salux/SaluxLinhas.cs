using System.Globalization;
using Oracle.ManagedDataAccess.Client;

namespace SMSMarica.Core.Integracoes.Pep.Estrategias.Salux;

/// <summary>Leitura segura de colunas do <see cref="OracleDataReader"/> (por nome/alias, cultura invariante).</summary>
internal static class Col
{
    public static string? Str(OracleDataReader r, string nome)
    {
        var i = r.GetOrdinal(nome);
        if (r.IsDBNull(i)) return null;
        var v = Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    public static long Long(OracleDataReader r, string nome)
    {
        var i = r.GetOrdinal(nome);
        return r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i), CultureInfo.InvariantCulture);
    }
}

/// <summary>Médico do Salux (tabela <c>medico</c> + especialidades agregadas).</summary>
internal sealed record MedicoLinha(
    long Cd, string? Nome, string? Crm, string? Uf, string? Conselho,
    string? Cpf, string? Cns, string? Rg, string? Orgao, string? Nasc, string? Sexo,
    string? Email, string? Ativo, string? Mae, string? Pai, string? Categoria, string? Cbo, string? Especialidade);

/// <summary>Paciente do Salux (tabela <c>paciente</c> + lookups resolvidos).</summary>
internal sealed record PacienteLinha(
    long Cd, string? Nome, string? Social, string? FlagSocial, string? Nasc, string? Sexo,
    string? Cpf, string? Cns, string? Rg, string? Orgao, string? Pis, string? Passaporte, string? Rne,
    string? Certidao, string? Sgh, string? Cem, string? Obito, string? Ativo,
    string? Logr, string? NrLogr, string? Compl, string? Bairro, string? Cep, string? Ref,
    string? Ddd, string? Fone, string? DddResp, string? FoneResp, string? Email,
    string? Mae, string? Pai, string? Conjuge, string? Responsavel, string? GrauParentesco,
    string? CdCor, string? CdNacionalidade, string? Pais, string? Profissao, string? Ocupacao,
    string? Peso, string? Altura, string? Sangue, string? Rh, string? Etnia, string? EntradaPais,
    string? Cidade, string? UfSigla, string? EstadoCivilDs, string? InstrucaoDs, string? ReligiaoDs, string? BarreiraDs);

/// <summary>Atendimento ambulatorial (BAA).</summary>
internal sealed record BaaLinha(
    long CdPaciente, long H, long Ano, long Nr,
    string? DtCheg, string? DtAtend, string? DtSaida,
    string? Cid, string? CidDs, string? Emerg, string? RiscoDs, string? Medico)
{
    public string Chave => $"{H}-{Ano}-{Nr}";
}

/// <summary>Item de prescrição (medicação) de um BAA.</summary>
internal sealed record PrescricaoLinha(
    long H, long Ano, long Nr,
    string? CdMat, string? Mat, string? Qt, string? Urg, string? Medico, string? Horario, string? Obs)
{
    public string ChaveBaa => $"{H}-{Ano}-{Nr}";
}

/// <summary>Documento clínico (EDOC) ligado a um BAA.</summary>
internal sealed record EdocLinha(
    long CdPaciente, long H, long Ano, long Idm, string? Modelo, string? Dt, string? Baa)
{
    public string ChaveDoc => $"{H}-{Ano}-{Idm}";
}

/// <summary>Item (rótulo/resposta) de um documento EDOC.</summary>
internal sealed record EdocItemLinha(long H, long Ano, long Idm, string? Label, string? Resp)
{
    public string ChaveDoc => $"{H}-{Ano}-{Idm}";
}
