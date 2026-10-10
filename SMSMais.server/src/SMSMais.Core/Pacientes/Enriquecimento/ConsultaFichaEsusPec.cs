using Microsoft.Extensions.Logging;
using SMSMais.Core.Integracoes.EsusPec;

namespace SMSMais.Core.Pacientes.Enriquecimento;

/// <summary>Com que conta entrar no e-SUS. A senha só vive nesta chamada — não é guardada nem logada.</summary>
public sealed record ContaEsusPec(string? BaseUrl, string Usuario, string Senha, bool Forcar, string? AcessoId);

/// <summary><see cref="OutraSessaoAberta"/> = não entrou para não derrubar a sessão de alguém.</summary>
public sealed record ResultadoConsultaEsusPec(bool OutraSessaoAberta, FichaCidadaoEsusPec? Ficha);

/// <summary>Uma consulta avulsa ao e-SUS PEC: entra, acha o cidadão, sai.</summary>
public interface IConsultaFichaEsusPec
{
    Task<ResultadoConsultaEsusPec> ConsultarAsync(ContaEsusPec conta, string documento, CancellationToken ct);
}

/// <summary>
/// Login → acesso → busca → logout, sempre nessa ordem e sempre com logout (o PEC é sessão única por
/// usuário: sessão esquecida aberta é a dona da conta barrada no posto). Só leitura — o cliente não
/// tem como gravar nada.
///
/// <para><b>Acesso:</b> a conta da plataforma já diz qual (parâmetro da credencial). A conta da
/// pessoa, não: escolhe-se a primeira lotação (ou estágio) em unidade de saúde e, se ela não puder
/// buscar cidadão, a próxima. Gestor/administrador municipal só vê relatório (laboratório, §3).</para>
/// </summary>
public sealed class ConsultaFichaEsusPec(ILogger<ConsultaFichaEsusPec> logger) : IConsultaFichaEsusPec
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(25);

    public async Task<ResultadoConsultaEsusPec> ConsultarAsync(ContaEsusPec conta, string documento, CancellationToken ct)
    {
        using var pec = new EsusPecCliente(conta.BaseUrl, Timeout, tentativas: 2);
        if (await pec.TentarLoginAsync(conta.Usuario, conta.Senha, conta.Forcar, ct) == ResultadoLoginEsusPec.OutraSessaoAberta)
            return new ResultadoConsultaEsusPec(true, null);

        try
        {
            if (!string.IsNullOrWhiteSpace(conta.AcessoId))
            {
                await pec.SelecionarAcessoAsync(conta.AcessoId, ct);
                return new ResultadoConsultaEsusPec(false, await pec.BuscarFichaAsync(documento, ct));
            }

            var lotacoes = (await pec.ListarAcessosAsync(ct))
                .Where(a => a.Tipo is "LOTACAO" or "ESTAGIO")
                .ToList();
            if (lotacoes.Count == 0)
            {
                throw new ErroEsusPec(
                    "O seu usuário do e-SUS não tem lotação em unidade de saúde — a consulta de cidadão precisa de uma.");
            }

            ErroEsusPec? ultimo = null;
            foreach (var acesso in lotacoes)
            {
                try
                {
                    await pec.SelecionarAcessoAsync(acesso.Id, ct);
                    return new ResultadoConsultaEsusPec(false, await pec.BuscarFichaAsync(documento, ct));
                }
                catch (ErroEsusPec e)
                {
                    ultimo = e;
                    logger.LogInformation("e-SUS: a lotação {Unidade} não consultou o cidadão ({Erro}); tentando a próxima.",
                        acesso.Unidade ?? acesso.Id, e.Message);
                }
            }
            throw ultimo!;
        }
        finally
        {
            await pec.LogoutAsync(CancellationToken.None);
        }
    }
}
