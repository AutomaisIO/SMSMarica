using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Cidadao.Push.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Cidadao.Push;

public sealed class PushCidadaoService(
    SmsMaisDbContext db,
    IIntegracaoCredencialService credenciais,
    IClienteFcm fcm,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<PushCidadaoService> logger) : IPushCidadaoService
{
    public const string ProvedorFcm = "fcm";
    public const string OrigemPainel = "painel";

    internal const int TituloMaximo = 65;
    internal const int MensagemMaxima = 240;
    internal const int TokenMaximo = 4096;
    private const int HistoricoNoPainel = 20;

    public async Task RegistrarAparelhoAsync(
        Guid pacienteId, Guid sessaoId, RegistrarDispositivoRequest request, CancellationToken ct = default)
    {
        var (token, plataforma) = ValidarAparelho(request);
        var agora = DateTime.UtcNow;

        // Primeiro a própria sessão: se ela não vale mais, a recusa não pode ter tirado o token de
        // ninguém.
        var gravadas = await db.CidadaoSessoes
            .Where(s => s.Id == sessaoId && s.CidadaoAcesso.PatientId == pacienteId && s.RevogadaEm == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.PushToken, token)
                .SetProperty(s => s.PushPlataforma, plataforma)
                .SetProperty(s => s.PushRegistradoEm, agora), ct);

        if (gravadas == 0) throw new UnauthorizedAccessException("Sessão encerrada.");

        // Sem filtrar por paciente de propósito: se quem usava o aparelho antes não conseguiu sair
        // (logout sem rede), a sessão dele ainda carrega este token — e as notificações DELE
        // cairiam no celular de quem entrou agora.
        await db.CidadaoSessoes
            .Where(s => s.PushToken == token && s.Id != sessaoId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.PushToken, (string?)null)
                .SetProperty(s => s.PushPlataforma, (string?)null)
                .SetProperty(s => s.PushRegistradoEm, (DateTime?)null), ct);
    }

    public async Task<AppCidadaoStatusDto> ObterStatusAsync(Guid pacienteId, CancellationToken ct = default)
    {
        // Sem decifrar, de propósito (a ficha abre isto sempre): "configurado" = segredo gravado e
        // ativo. Credencial que não decifra aparece no Testar e no envio, não aqui.
        var credencial = await credenciais.ObterAsync(ProvedorFcm, ct);

        var aparelhos = await AparelhosAtivos(pacienteId, DateTime.UtcNow)
            .OrderByDescending(s => s.PushRegistradoEm)
            .Select(s => new AparelhoAppCidadaoDto(
                s.Id,
                s.PushPlataforma ?? string.Empty,
                s.PushRegistradoEm ?? s.CriadaEm,
                s.CriadaEm,
                s.Dispositivo))
            .ToListAsync(ct);

        var notificacoes = await db.CidadaoNotificacoes.AsNoTracking()
            .Where(n => n.PacienteId == pacienteId)
            .OrderByDescending(n => n.CriadoEm)
            .Take(HistoricoNoPainel)
            .Select(n => new NotificacaoAppCidadaoDto(
                n.Id,
                n.Titulo,
                n.Mensagem,
                n.Rota,
                n.CriadoEm,
                db.Usuarios.Where(u => u.Id == n.EnviadoPor).Select(u => u.NomeCompleto).FirstOrDefault(),
                n.Aparelhos,
                n.Entregues,
                n.Falha))
            .ToListAsync(ct);

        return new AppCidadaoStatusDto(credencial.ClientSecretDefinido && credencial.Ativo, aparelhos, notificacoes);
    }

    public async Task<EnvioNotificacaoAppDto> EnviarAsync(
        Guid pacienteId, EnviarNotificacaoAppRequest request, CancellationToken ct = default)
    {
        var (titulo, mensagem, rota) = ValidarEnvio(request);
        var conta = await LerContaServicoAsync(ct);

        var aparelhos = await AparelhosAtivos(pacienteId, DateTime.UtcNow)
            .Select(s => new { s.Id, Token = s.PushToken!, Plataforma = s.PushPlataforma ?? string.Empty })
            .ToListAsync(ct);
        if (aparelhos.Count == 0)
            throw new ConflitoException("push.sem_aparelho", "Este paciente não tem o app com as notificações ativas.");

        var notificacaoId = Guid.CreateVersion7();
        var dados = new Dictionary<string, string>();
        if (rota is not null) dados["rota"] = rota;
        dados["notificacaoId"] = notificacaoId.ToString();

        // Daqui em diante o envio vai até o fim, sem o token de cancelamento da requisição: fechar a
        // tela no meio deixaria aparelho notificado sem a linha de histórico que prova o envio.
        var semCancelar = CancellationToken.None;

        DesfechoEnvioFcm? falhaDoToken = null;
        string? accessToken = null;
        try
        {
            accessToken = await fcm.ObterAccessTokenAsync(conta, ct: semCancelar);
        }
        catch (FalhaTokenFcmException ex)
        {
            falhaDoToken = ex.Desfecho;
            Registrar(ex.Desfecho, pacienteId, "todos os aparelhos");
        }

        var resultados = new List<ResultadoEnvioAparelhoDto>(aparelhos.Count);
        var desfechos = new List<DesfechoEnvioFcm>(aparelhos.Count);
        foreach (var a in aparelhos)
        {
            var desfecho = falhaDoToken
                ?? await fcm.EnviarAsync(conta, accessToken!, new MensagemFcm(a.Token, titulo, mensagem, dados), semCancelar);
            if (falhaDoToken is null) Registrar(desfecho, pacienteId, $"aparelho {a.Plataforma}");

            if (desfecho.AparelhoRemovido)
            {
                // Só se o token ainda for o mesmo: o app pode ter registrado um novo nesse meio tempo.
                await db.CidadaoSessoes
                    .Where(s => s.Id == a.Id && s.PushToken == a.Token)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(s => s.PushToken, (string?)null)
                        .SetProperty(s => s.PushPlataforma, (string?)null)
                        .SetProperty(s => s.PushRegistradoEm, (DateTime?)null), semCancelar);
            }

            desfechos.Add(desfecho);
            resultados.Add(new ResultadoEnvioAparelhoDto(
                a.Plataforma, desfecho.Entregue, desfecho.Detalhe, desfecho.AparelhoRemovido));
        }

        var entregues = desfechos.Count(d => d.Entregue);
        db.CidadaoNotificacoes.Add(new CidadaoNotificacao
        {
            Id = notificacaoId,
            PacienteId = pacienteId,
            Titulo = titulo,
            Mensagem = mensagem,
            Rota = rota,
            Origem = OrigemPainel,
            EnviadoPor = usuarioAtual.UsuarioId,
            CriadoEm = DateTime.UtcNow,
            Aparelhos = aparelhos.Count,
            Entregues = entregues,
            Falha = ResumirFalha(aparelhos.Count, desfechos),
        });
        await db.SaveChangesAsync(semCancelar);

        return new EnvioNotificacaoAppDto(notificacaoId, aparelhos.Count, entregues, resultados);
    }

    public async Task<TesteCredencialFcmDto> TestarCredencialAsync(CancellationToken ct = default)
    {
        IntegracaoCredencialContexto contexto;
        try
        {
            contexto = await credenciais.ObterContextoAsync(ProvedorFcm, ct);
        }
        catch (ValidacaoException)
        {
            return new TesteCredencialFcmDto(false, null,
                "A credencial do Firebase ainda não foi gravada ou está desativada.");
        }
        catch (Exception ex) when (EhCredencialIlegivel(ex))
        {
            logger.LogWarning("Push do app do cidadão: o Testar não decifrou a credencial do Firebase ({Erro}).", ex.Message);
            return new TesteCredencialFcmDto(false, null, MensagemCredencialIlegivel);
        }

        ContaServicoFcm conta;
        try
        {
            conta = ContaServicoFcm.Ler(contexto.ClientSecret);
        }
        catch (ValidacaoException ex)
        {
            return new TesteCredencialFcmDto(false, null, ex.Message);
        }

        string accessToken;
        try
        {
            accessToken = await fcm.ObterAccessTokenAsync(conta, renovar: true, ct);
        }
        catch (FalhaTokenFcmException ex)
        {
            var mensagem = ex.Desfecho.CredencialRecusada
                ? $"O Google recusou a credencial: {ex.Message}"
                : ex.Message;
            return new TesteCredencialFcmDto(false, conta.ProjectId, mensagem);
        }

        // O token só prova que a conta existe e a chave confere. API desligada no Google Cloud ou
        // conta sem papel de envio só aparecem no messages:send — daí o envio de validação.
        var validacao = await fcm.ValidarEnvioAsync(conta, accessToken, ct);
        if (!validacao.Ok)
            logger.LogWarning("Push do app do cidadão: o Testar da credencial falhou no envio de validação — {Tecnico}.",
                validacao.Tecnico);
        return new TesteCredencialFcmDto(validacao.Ok, conta.ProjectId, validacao.Mensagem);
    }

    /// <summary>
    /// A fonte da verdade de "quem recebe": sessão viva (não revogada, não expirada), acesso ativo
    /// e token presente. Logout e expiração cortam o envio aqui, mesmo se algum caminho de revogação
    /// esquecer de apagar o token.
    /// </summary>
    private IQueryable<CidadaoSessao> AparelhosAtivos(Guid pacienteId, DateTime agora) =>
        db.CidadaoSessoes.AsNoTracking()
            .Where(s => s.CidadaoAcesso.PatientId == pacienteId
                && s.CidadaoAcesso.Ativo
                && s.RevogadaEm == null
                && s.ExpiraEm > agora
                && s.PushToken != null);

    private async Task<ContaServicoFcm> LerContaServicoAsync(CancellationToken ct)
    {
        IntegracaoCredencialContexto contexto;
        try
        {
            contexto = await credenciais.ObterContextoAsync(ProvedorFcm, ct);
        }
        catch (ValidacaoException)
        {
            throw NaoConfigurado();
        }
        catch (Exception ex) when (EhCredencialIlegivel(ex))
        {
            logger.LogError("Push do app do cidadão: a credencial do Firebase gravada não decifra neste servidor ({Erro}).", ex.Message);
            throw new ValidacaoException("push.credencial_ilegivel", MensagemCredencialIlegivel);
        }

        if (string.IsNullOrWhiteSpace(contexto.ClientSecret)) throw NaoConfigurado();

        try
        {
            return ContaServicoFcm.Ler(contexto.ClientSecret);
        }
        catch (ValidacaoException ex)
        {
            // Gravar já confere o JSON; chegar aqui é credencial antiga ou corrompida no cofre.
            logger.LogError("Push do app do cidadão: a credencial do Firebase gravada é inválida ({Motivo}).", ex.Message);
            throw NaoConfigurado();
        }
    }

    private static ValidacaoException NaoConfigurado() => new("push.nao_configurado",
        "O envio de notificações ao app ainda não foi configurado (Integrações → Firebase).");

    internal const string MensagemCredencialIlegivel =
        "A credencial gravada não pode ser lida por este servidor (foi cifrada em outro ambiente). Use Limpar e cole o JSON de novo.";

    /// <summary>
    /// Segredo cifrado com outra chave do Data Protection (banco copiado entre ambientes, bancada):
    /// o cofre lança <see cref="CryptographicException"/>. Valor que nem é um texto cifrado (linha
    /// semeada à mão) também chega assim — o <c>Unprotect</c> de string embrulha o
    /// <see cref="FormatException"/> do Base64 —, então nada além dela é tratado. Só aqui, no push:
    /// o cofre continua lançando para os outros provedores.
    /// </summary>
    private static bool EhCredencialIlegivel(Exception ex) => ex is CryptographicException;

    private void Registrar(DesfechoEnvioFcm d, Guid pacienteId, string aparelho)
    {
        switch (d.Gravidade)
        {
            case GravidadeFalhaFcm.Erro:
                logger.LogError("Push do app do cidadão recusado — {Tecnico} (paciente {PacienteId}, {Aparelho}).",
                    d.Tecnico, pacienteId, aparelho);
                break;
            case GravidadeFalhaFcm.Aviso:
                logger.LogWarning("Push do app do cidadão sem resposta do Firebase — {Tecnico} (paciente {PacienteId}, {Aparelho}).",
                    d.Tecnico, pacienteId, aparelho);
                break;
            default:
                if (d.AparelhoRemovido)
                    logger.LogInformation("Push do app do cidadão: token descartado — {Tecnico} (paciente {PacienteId}, {Aparelho}).",
                        d.Tecnico, pacienteId, aparelho);
                break;
        }
    }

    internal static (string Token, string Plataforma) ValidarAparelho(RegistrarDispositivoRequest request)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        var plataforma = request.Plataforma?.Trim().ToLowerInvariant() ?? string.Empty;

        var erros = new Dictionary<string, string[]>();
        if (token.Length == 0) erros["token"] = ["Informe o token do aparelho."];
        else if (token.Length > TokenMaximo) erros["token"] = [$"O token do aparelho tem no máximo {TokenMaximo} caracteres."];
        if (plataforma is not ("android" or "ios")) erros["plataforma"] = ["Plataforma deve ser android ou ios."];

        return erros.Count > 0 ? throw new ValidacaoException(erros) : (token, plataforma);
    }

    internal static (string Titulo, string Mensagem, string? Rota) ValidarEnvio(EnviarNotificacaoAppRequest request)
    {
        var titulo = request.Titulo?.Trim() ?? string.Empty;
        var mensagem = request.Mensagem?.Trim() ?? string.Empty;
        var rota = string.IsNullOrWhiteSpace(request.Rota) ? null : request.Rota.Trim();

        var erros = new Dictionary<string, string[]>();
        if (titulo.Length == 0) erros["titulo"] = ["Informe o título."];
        else if (titulo.Length > TituloMaximo) erros["titulo"] = [$"O título tem no máximo {TituloMaximo} caracteres."];
        if (mensagem.Length == 0) erros["mensagem"] = ["Escreva a mensagem."];
        else if (mensagem.Length > MensagemMaxima) erros["mensagem"] = [$"A mensagem tem no máximo {MensagemMaxima} caracteres."];
        if (rota is not null && !RotasAppCidadao.EhPermitida(rota))
            erros["rota"] = ["Escolha uma das telas do app da lista."];

        return erros.Count > 0 ? throw new ValidacaoException(erros) : (titulo, mensagem, rota);
    }

    /// <summary>"1 de 2 aparelhos recusou: app desinstalado". Null quando todos aceitaram.</summary>
    internal static string? ResumirFalha(int aparelhos, IReadOnlyList<DesfechoEnvioFcm> desfechos)
    {
        var falhas = desfechos.Where(d => !d.Entregue).ToList();
        if (falhas.Count == 0) return null;

        var motivos = string.Join("; ", falhas.Select(f => f.Resumo ?? "motivo não informado").Distinct());
        var texto = $"{falhas.Count} de {aparelhos} {(aparelhos == 1 ? "aparelho" : "aparelhos")} "
            + $"{(falhas.Count == 1 ? "recusou" : "recusaram")}: {motivos}";
        return texto.Length <= 500 ? texto : texto[..499] + "…";
    }
}
