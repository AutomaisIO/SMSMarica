using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Alertas;

namespace SMSMais.Core.Integracoes.SisregWeb.Rede;

/// <summary>
/// Vigia o endereço do SISREG. Em 09/10/2026 o <c>sisregiii.saude.gov.br</c> passou para trás do F5
/// (189.28.130.13 → 159.60.146.75) sem aviso; o túnel <c>wg-eveo</c> só desviava o IP antigo, a
/// produção saiu pelos EUA e o login passou a falhar — parecia que o IP da Eveo tinha sido bloqueado.
///
/// <para><b>Divisão de trabalho</b> (docs/sisreg-egress.md §Verificador): quem CONSERTA a rota é o
/// timer <c>sisreg-egress-verificar</c> do servidor (root, a cada 1 min — a API roda sem privilégio e
/// não mexe em rota). Este worker só OBSERVA, por conta própria: resolve o DNS, lê por qual interface
/// a rota sai, grava o período de cada IP (<see cref="IEnderecoSisregService"/>) e avisa no celular.
/// Observar independente do timer é o que pega o timer quebrado.</para>
///
/// <para><b>Quando avisa</b> (<see cref="Decidir"/>): quando entra IP novo (não na primeira
/// verificação da história — aí só registra); quando a rota está fora do túnel em DUAS verificações
/// seguidas (a primeira pode ser o minuto entre a troca e o conserto do timer); e quando volta ao
/// túnel depois de um aviso. DNS que não resolve só vai ao log: o próprio SISREG cai junto e os
/// avisos do sincronismo já cobrem.</para>
/// </summary>
public sealed class VerificadorEnderecoSisregWorker(
    IServiceScopeFactory scopes,
    ISondaEnderecoSisreg sonda,
    EstadoEnderecoSisreg estado,
    IAlertaPlataforma alerta,
    IOptions<EnderecoSisregOpcoes> opcoes,
    ILogger<VerificadorEnderecoSisregWorker> logger) : BackgroundService
{
    private const string Grupo = "Sincronismo";

    private bool _foraNaAnterior;
    private bool _foraAvisado;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SISREG_ENDERECO: a verificação do endereço do SISREG falhou.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, opcoes.Value.IntervaloMinutos)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var o = opcoes.Value;
        if (!o.Habilitado) return;

        var agora = DateTime.UtcNow;
        ObservacaoEndereco observacao;
        try
        {
            observacao = await sonda.ObservarAsync(o.Host, o.InterfaceTunel, ct);
        }
        catch (SocketException ex)
        {
            estado.Registrar(new UltimaVerificacaoEndereco(agora, [], null, $"O DNS não resolveu {o.Host}: {ex.Message}"));
            logger.LogWarning("SISREG_ENDERECO: o DNS não resolveu {Host}: {Erro}", o.Host, ex.Message);
            return;
        }

        if (observacao.Ips.Count == 0)
        {
            estado.Registrar(new UltimaVerificacaoEndereco(agora, [], observacao.TunelEsperado, $"O DNS não devolveu IPv4 para {o.Host}."));
            logger.LogWarning("SISREG_ENDERECO: o DNS não devolveu IPv4 para {Host}.", o.Host);
            return;
        }

        MudancaEndereco mudanca;
        using (var scope = scopes.CreateScope())
        {
            mudanca = await scope.ServiceProvider.GetRequiredService<IEnderecoSisregService>()
                .RegistrarAsync(observacao, agora, ct);
        }
        estado.Registrar(new UltimaVerificacaoEndereco(agora, observacao.Ips, observacao.TunelEsperado, null));

        var fora = EnderecoSisregService.IpsForaDoTunel(observacao.Ips, observacao.TunelEsperado);
        var decisao = Decidir(mudanca, foraAgora: fora.Count > 0, _foraNaAnterior, _foraAvisado);
        _foraNaAnterior = fora.Count > 0;

        if (mudanca.PrimeiraVez)
            logger.LogInformation("SISREG_ENDERECO: primeira verificação — {Host} em {Ips}.", o.Host, string.Join(", ", observacao.Ips.Select(i => i.Ip)));

        if (decisao.AvisarTroca)
        {
            var (titulo, detalhe) = MontarTroca(o.Host, mudanca, observacao);
            logger.LogWarning("SISREG_ENDERECO: {Titulo} — {Detalhe}", titulo, detalhe);
            alerta.Reportar(new EventoAlerta(AlertaCatalogo.SisregIpMudou, titulo, detalhe)
            {
                Rotulo = "SISREG trocou de IP",
                Grupo = Grupo,
            });
        }

        if (decisao.AvisarForaDoTunel)
        {
            var (titulo, detalhe) = MontarForaDoTunel(o.Host, fora, observacao.TunelEsperado!);
            logger.LogWarning("SISREG_ENDERECO: {Titulo} — {Detalhe}", titulo, detalhe);
            alerta.Reportar(new EventoAlerta(AlertaCatalogo.SisregForaDoTunel, titulo, detalhe)
            {
                Rotulo = "SISREG fora do túnel",
                Grupo = Grupo,
            });
            _foraAvisado = true;
        }

        if (decisao.AvisarTunelOk)
        {
            logger.LogInformation("SISREG_ENDERECO: a rota do SISREG voltou ao túnel.");
            alerta.Reportar(new EventoAlerta(AlertaCatalogo.SisregTunelOk, "SISREG de volta ao túnel",
                $"A rota até {o.Host} ({string.Join(", ", observacao.Ips.Select(i => i.Ip))}) voltou a sair pelo "
                + $"{observacao.TunelEsperado ?? "túnel"}.")
            {
                Rotulo = "SISREG de volta ao túnel",
                Grupo = Grupo,
            });
            _foraAvisado = false;
        }
    }

    public sealed record DecisaoAvisos(bool AvisarTroca, bool AvisarForaDoTunel, bool AvisarTunelOk);

    /// <summary>A regra de quando mandar. Pura, para teste.</summary>
    /// <param name="foraAgora">Algum IP atual com a rota lida fora do túnel nesta verificação.</param>
    /// <param name="foraNaAnterior">Idem na verificação anterior — o aviso de "fora" exige duas seguidas.</param>
    /// <param name="foraAvisado">Já foi mandado o aviso de "fora" e ainda não o de "voltou".</param>
    public static DecisaoAvisos Decidir(MudancaEndereco mudanca, bool foraAgora, bool foraNaAnterior, bool foraAvisado) =>
        new(
            AvisarTroca: !mudanca.PrimeiraVez && mudanca.Entraram.Count > 0,
            AvisarForaDoTunel: foraAgora && foraNaAnterior && !foraAvisado,
            AvisarTunelOk: !foraAgora && foraAvisado);

    /// <summary>O texto do aviso de troca. Público para teste: é o que o Bernardo lê no celular.</summary>
    public static (string Titulo, string Detalhe) MontarTroca(string host, MudancaEndereco mudanca, ObservacaoEndereco observacao)
    {
        var novos = string.Join(", ", mudanca.Entraram);
        var titulo = $"o SISREG passou a responder em {novos}";
        var detalhe = $"{host} agora resolve para {novos}"
                      + (mudanca.Anteriores.Count > 0 ? $" (antes: {string.Join(", ", mudanca.Anteriores)})" : "")
                      + ". ";
        detalhe += observacao.TunelEsperado is { } tunel
            ? $"O verificador do servidor põe o IP novo no túnel {tunel} em até 1 min; se o card do SISREG em "
              + "Integrações não mostrar \"saindo pelo túnel\", o login no SISREG falha."
            : "Este servidor sai direto, sem túnel: nada a fazer.";
        return (titulo, detalhe);
    }

    /// <summary>O texto do aviso de rota fora do túnel. Público para teste.</summary>
    public static (string Titulo, string Detalhe) MontarForaDoTunel(string host, IReadOnlyList<IpObservado> fora, string tunel)
    {
        var lista = string.Join(", ", fora.Select(i => $"{i.Ip} (sai por {i.InterfaceRota})"));
        return (
            $"o SISREG está saindo fora do túnel {tunel}",
            $"A rota até {host} — {lista} — não passa pelo {tunel}: de fora do Brasil o SISREG não responde e o "
            + "login falha (\"Não foi possível autenticar\"). Conferir no servidor o timer sisreg-egress-verificar "
            + "(journalctl -t sisreg-egress) e docs/sisreg-egress.md.");
    }
}
