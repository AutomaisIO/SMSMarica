using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Core.Integracoes.SisregWeb;

/// <summary>Consulta de paciente por CNS ou CPF no SISREG III (CADSUS / <c>cadweb50</c>).</summary>
public interface IConsultaCnsService
{
    Task<ConsultaCnsRespostaDto> ConsultarPorCnsAsync(string cns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta por CPF — o campo "CPF/CNS" do CADSUS aceita os dois documentos
    /// (validado ao vivo em 2026-07-01). Base do motor de fallback do proxy CPF.
    /// </summary>
    Task<ConsultaCnsRespostaDto> ConsultarPorCpfAsync(string cpf, CancellationToken cancellationToken = default);
}

public sealed class ConsultaCnsService(ISisregWebSessao sessao) : IConsultaCnsService
{
    public async Task<ConsultaCnsRespostaDto> ConsultarPorCnsAsync(string cns, CancellationToken cancellationToken = default)
    {
        var digitos = new string([.. (cns ?? string.Empty).Where(char.IsDigit)]);
        if (digitos.Length != 15)
        {
            throw new ValidacaoException("sisreg.cns_invalido", "CNS deve ter 15 dígitos.");
        }
        return await ConsultarPorDocumentoAsync(
            digitos, "Nenhum paciente encontrado no SISREG para este CNS.", cancellationToken);
    }

    public async Task<ConsultaCnsRespostaDto> ConsultarPorCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        var digitos = new string([.. (cpf ?? string.Empty).Where(char.IsDigit)]);
        if (digitos.Length != 11)
        {
            throw new ValidacaoException("sisreg.cpf_invalido", "CPF deve ter 11 dígitos.");
        }
        return await ConsultarPorDocumentoAsync(
            digitos, "Nenhum paciente encontrado no SISREG para este CPF.", cancellationToken);
    }

    private async Task<ConsultaCnsRespostaDto> ConsultarPorDocumentoAsync(
        string digitos, string erroNaoEncontrado, CancellationToken cancellationToken)
    {
        // Por documento exato (CPF ou CNS), o CADSUS abre a ficha completa direto com
        // etapa=DETALHAR (LISTAR devolveria a lista; validado ao vivo em 2026-07-01).
        var campos = new Dictionary<string, string>
        {
            ["nu_cns"] = digitos, // o campo aceita CPF OU CNS
            ["nome_paciente"] = string.Empty,
            ["nome_mae"] = string.Empty,
            ["dt_nascimento"] = string.Empty,
            ["uf_nasc"] = string.Empty,
            ["mun_nasc"] = string.Empty,
            ["uf_res"] = string.Empty,
            ["mun_res"] = string.Empty,
            ["sexo"] = string.Empty,
            ["standalone"] = "1",
            ["etapa"] = "DETALHAR",
            ["url"] = string.Empty,
        };

        var html = await sessao.PostFormAsync("/cgi-bin/cadweb50?standalone=1", campos, cancellationToken);

        var registro = CadsusHtmlParser.Parse(html)
            ?? throw new NaoEncontradoException("sisreg.paciente_nao_encontrado", erroNaoEncontrado);

        // Endereço e telefone do CADSUS são mapeados no registro mas NÃO retornados —
        // vêm quase sempre desatualizados; não auto-preenchemos. (Ver CadsusRegistro.)
        return new ConsultaCnsRespostaDto(
            Cns: registro.Cns.Length == 15 ? registro.Cns : digitos.Length == 15 ? digitos : registro.Cns,
            Cpf: registro.Cpf,
            Nome: registro.Nome,
            Sexo: registro.Sexo,
            DataNascimento: registro.DataNascimento,
            NomeMae: registro.NomeMae);
    }
}
