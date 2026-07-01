using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Core.Integracoes.SisregWeb;

/// <summary>Consulta de paciente por CNS no SISREG III (CADSUS / <c>cadweb50</c>).</summary>
public interface IConsultaCnsService
{
    Task<ConsultaCnsRespostaDto> ConsultarPorCnsAsync(string cns, CancellationToken cancellationToken = default);
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

        // Por CNS exato, o CADSUS abre a ficha completa direto com etapa=DETALHAR
        // (LISTAR devolveria a lista de resultados; validado ao vivo em 2026-07-01).
        var campos = new Dictionary<string, string>
        {
            ["nu_cns"] = digitos,
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
            ?? throw new NaoEncontradoException(
                "sisreg.paciente_nao_encontrado",
                "Nenhum paciente encontrado no SISREG para este CNS.");

        // Endereço e telefone do CADSUS são mapeados no registro mas NÃO retornados —
        // vêm quase sempre desatualizados; não auto-preenchemos. (Ver CadsusRegistro.)
        return new ConsultaCnsRespostaDto(
            Cns: registro.Cns.Length == 15 ? registro.Cns : digitos,
            Cpf: registro.Cpf,
            Nome: registro.Nome,
            Sexo: registro.Sexo,
            DataNascimento: registro.DataNascimento,
            NomeMae: registro.NomeMae);
    }
}
