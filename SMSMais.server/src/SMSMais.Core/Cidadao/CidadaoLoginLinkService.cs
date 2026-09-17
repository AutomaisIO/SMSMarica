using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Laudos.Configuracao;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Cidadao;

/// <summary>
/// "Magic-link" de login do cidadão: gera um token de uso único e validade curta
/// (dias configuráveis) para autenticar em 1 clique via WhatsApp, e o troca por uma
/// sessão normal do cidadão. Pensado para idosos, sem abrir mão da LGPD (uso único,
/// TTL curto, o app limpa o token da URL após entrar).
/// </summary>
public interface ICidadaoLoginLinkService
{
    /// <summary>Gera um magic-link que loga o paciente da solicitação e cai no exame.
    /// <paramref name="destino"/> troca a rota de chegada (default "/exames"). A validade
    /// respeita a config, mas nunca expira ANTES da DataAgendada (o botão "Confirmar" do
    /// WhatsApp precisa funcionar até o dia do exame; teto 30 dias).</summary>
    Task<MagicLinkDto> GerarParaSolicitacaoAsync(
        Guid solicitacaoExameId, string? destino = null, bool exigeConfirmacaoCpf = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Troca o token por sessão (uso único). <c>null</c> se inválido/usado/expirado.
    /// <para>Links clínicos (<see cref="CidadaoLoginLink.ExigeConfirmacaoCpf"/>) exigem o
    /// <paramref name="cpf"/> do titular: sem ele a resposta é só o DESAFIO e o token NÃO é
    /// consumido; com CPF errado, conta a tentativa e queima o link na 3ª.</para>
    /// </summary>
    Task<RespostaMagicLinkDto?> TrocarAsync(
        Guid token, string? dispositivo, string? ip, string? cpf = null,
        CancellationToken cancellationToken = default);
}

public sealed class CidadaoLoginLinkService(
    SmsMaisDbContext db,
    ILaudoConfiguracaoService configuracaoLaudo,
    IPacientesService pacientes,
    ICidadaoSessaoService sessoes,
    Telefones.ITelefoneValidacaoService telefones,
    IUsuarioAtualAccessor usuarioAtual,
    IConfiguration configuration) : ICidadaoLoginLinkService
{
    private const string DestinoPadrao = "/exames";

    /// <summary>Tentativas de CPF antes de o link ser queimado.</summary>
    private const int MaxTentativasCpf = 3;

    public async Task<MagicLinkDto> GerarParaSolicitacaoAsync(
        Guid solicitacaoExameId, string? destino = null, bool exigeConfirmacaoCpf = false,
        CancellationToken cancellationToken = default)
    {
        // O parâmetro pode ser o id PÚBLICO do exame de imagem (ExameImagem.Id) OU já o id da
        // solicitação (regulação SISREG, que NÃO tem ExameImagem). Traduz para o id da espinha.
        var solicitacaoSpineId = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == solicitacaoExameId).Select(e => (Guid?)e.SolicitacaoId)
            .FirstOrDefaultAsync(cancellationToken) ?? solicitacaoExameId;

        // Carrega a SOLICITAÇÃO direto — NÃO pelo loader de imagem, que exigiria um ExameImagem
        // (quebrava a confirmação de agendamentos de regulação sem exame de imagem).
        var sol = await db.Solicitacoes.AsNoTracking()
            .Where(x => x.Id == solicitacaoSpineId && x.ExcluidoEm == null)
            .Select(x => new { x.PacienteId, x.DataAgendada })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Solicitacao), solicitacaoSpineId);

        var p = await pacientes.ObterPorIdAsync(sol.PacienteId, cancellationToken);
        var cpf = Digitos(p.Cpf);
        if (cpf.Length != 11)
            throw new ConflitoException("magiclink.sem_cpf", "Paciente sem CPF válido para gerar o link de acesso.");

        var cfg = await configuracaoLaudo.ObterAsync(cancellationToken);
        var dias = Math.Clamp(cfg.MagicLinkValidadeDias, 1, 30);
        // O link da notificação de agendamento precisa viver até o exame (senão o botão
        // "Confirmar" do WhatsApp quebra antes do dia marcado).
        if (sol.DataAgendada is { } da && da > DateTime.UtcNow)
        {
            var diasAteExame = (int)Math.Ceiling((da - DateTime.UtcNow).TotalDays) + 1;
            dias = Math.Clamp(Math.Max(dias, diasAteExame), 1, 30);
        }

        var link = new CidadaoLoginLink
        {
            Id = Guid.CreateVersion7(),
            PatientId = sol.PacienteId,
            Cpf = cpf,
            Destino = string.IsNullOrWhiteSpace(destino) ? DestinoPadrao : destino,
            SolicitacaoId = solicitacaoSpineId,
            ExigeConfirmacaoCpf = exigeConfirmacaoCpf,
            ExpiraEm = DateTime.UtcNow.AddDays(dias),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.CidadaoLoginLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);

        return new MagicLinkDto(link.Id, MontarUrl(link.Id), link.ExpiraEm);
    }

    /// <summary>
    /// Confirma a presença da solicitação do link (o clique no botão do WhatsApp É a confirmação)
    /// e devolve o resumo para a tela. IDEMPOTENTE: já confirmada devolve o resumo sem regravar
    /// (<c>ConfirmadaAgora=false</c>). NÃO chama SaveChanges — quem chama decide a transação.
    /// Devolve <c>null</c> quando não há solicitação, ela sumiu, ou o agendamento já passou/foi
    /// cancelado (nesses casos não há o que confirmar).
    /// </summary>
    private async Task<ConfirmacaoAgendamentoDto?> ConfirmarPresencaAsync(
        Guid? solicitacaoId, CancellationToken cancellationToken)
    {
        if (solicitacaoId is not { } id) return null;

        var s = await db.Solicitacoes
            .Include(x => x.ExameImagem!).ThenInclude(e => e.TipoExame)
            .Include(x => x.UnidadeExecutante)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken);
        if (s is null) return null;

        var confirmadaAgora = false;
        // Mesma régua do app (CidadaoClinicoService): vale enquanto o exame for do dia
        // corrente de Brasília. Exigir hora futura fazia o clique no botão do WhatsApp,
        // no dia do exame depois do horário, não confirmar nada — e em silêncio.
        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
            && s.DataAgendada is { } da && da >= FusoBrasilia.InicioDoDiaAtualEmUtc())
        {
            s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
            s.ConfirmadoEm = DateTime.UtcNow;
            s.ConfirmadoCanal = "whatsapp-link";
            s.AtualizadoEm = DateTime.UtcNow;
            confirmadaAgora = true;
        }

        if (!confirmadaAgora && s.StatusConfirmacao != StatusConfirmacaoAgendamento.Confirmada)
            return null; // cancelada pelo paciente ou fora da régua — nada a mostrar

        return new ConfirmacaoAgendamentoDto(
            s.ExameImagem?.Id ?? s.Id,
            s.ExameImagem?.TipoExame?.Nome ?? "Exame",
            s.DataAgendada,
            s.UnidadeExecutante?.Nome,
            confirmadaAgora);
    }

    public async Task<RespostaMagicLinkDto?> TrocarAsync(
        Guid token, string? dispositivo, string? ip, string? cpf = null,
        CancellationToken cancellationToken = default)
    {
        var link = await db.CidadaoLoginLinks.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == token, cancellationToken);
        if (link is null) return null; // token inexistente → 410 (app manda pro login)

        var agora = DateTime.UtcNow;
        var usadoIp = ip is { Length: > 64 } ? ip[..64] : ip;

        // GATE DE CPF — antes de qualquer consumo. Vale para os links que carregam resultado
        // clínico: possuir o link deixa de SER a credencial. Um link entregue à pessoa errada
        // (troca de identidade no exame, número desatualizado) não abre o prontuário alheio.
        if (link.ExigeConfirmacaoCpf)
        {
            // Link clínico gasto/expirado NÃO devolve nem o destino: "o link morre" e a
            // recepção reenvia. (Links não-clínicos mantêm o facilitador do aparelho original.)
            if (link.UsadoEm is not null || link.ExpiraEm <= agora) return null;

            var informado = Digitos(cpf);
            var restantes = Math.Max(0, MaxTentativasCpf - link.TentativasCpf);

            // Sem CPF: DESAFIO. Nada é consumido e nada é revelado — nem nome, nem CPF
            // mascarado, nem destino. Quem não é o titular não descobre de quem é o exame.
            if (informado.Length == 0)
                return new RespostaMagicLinkDto(
                    Token: null, Paciente: null, Destino: string.Empty,
                    ConfirmacaoAgendamento: null,
                    RequerConfirmacaoCpf: true, TentativasRestantes: restantes);

            if (!string.Equals(informado, link.Cpf, StringComparison.Ordinal))
            {
                var tentativas = link.TentativasCpf + 1;
                var queimou = tentativas >= MaxTentativasCpf;
                // Queimar = antecipar a expiração (mesmo mecanismo de revogação do "Reenviar").
                await db.CidadaoLoginLinks
                    .Where(x => x.Id == token)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.TentativasCpf, tentativas)
                        .SetProperty(x => x.ExpiraEm, x => queimou ? agora : x.ExpiraEm),
                        cancellationToken);

                if (queimou) return null; // 410 — a tela manda procurar a unidade
                return new RespostaMagicLinkDto(
                    Token: null, Paciente: null, Destino: string.Empty,
                    ConfirmacaoAgendamento: null,
                    RequerConfirmacaoCpf: true,
                    TentativasRestantes: MaxTentativasCpf - tentativas);
            }
            // CPF confere → segue para o consumo atômico normal.
        }

        // USO ÚNICO ATÔMICO: uma única requisição consegue marcar usado_em. Se a pessoa
        // compartilhar o link, quem clicar depois (ou um 2º clique simultâneo) recebe 0 linhas.
        var reivindicadas = await db.CidadaoLoginLinks
            .Where(x => x.Id == token && x.UsadoEm == null && x.ExpiraEm > agora)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.UsadoEm, agora)
                .SetProperty(x => x.UsadoIp, usadoIp), cancellationToken);

        // Clique no link = paciente VISUALIZOU a comunicação que o carregava (✓✓ azul).
        // Direto no banco, idempotente e best-effort (nunca impede o login).
        if (reivindicadas > 0)
        {
            await db.ComunicacoesPaciente
                .Where(c => c.LoginLinkId == token && c.VisualizadoEm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.VisualizadoEm, agora), cancellationToken);
        }

        if (reivindicadas == 0)
        {
            // Token já usado/expirado (mas existe): NUNCA autentica. Devolve só o destino,
            // sem JWT — o app abre o exame direto SE já estiver autenticado (facilitador no
            // aparelho original) e, se NÃO estiver, cai no login. Aparelho sem sessão jamais
            // autentica com token gasto (mesmo que a pessoa compartilhe o link).
            //
            // MAS o botão "Sim! Confirmo a minha presença" tem de ser IDEMPOTENTE: quem clica de
            // novo (ou clica pela 1ª vez num link já consumido por um preview) precisa ver
            // "presença confirmada", não ser jogado no login/OTP — a mensagem só chega a número
            // verificado ou a quem passou na verificação cadastral, e confirmar comparecimento não
            // expõe nada clínico. Vale só para link de AGENDAMENTO (sem gate de CPF).
            var confirmacaoRepetida = link.ExigeConfirmacaoCpf
                ? null
                : await ConfirmarPresencaAsync(link.SolicitacaoId, cancellationToken);
            if (confirmacaoRepetida is not null) await db.SaveChangesAsync(cancellationToken);

            return new RespostaMagicLinkDto(
                Token: null, Paciente: null,
                Destino: string.IsNullOrWhiteSpace(link.Destino) ? "/" : link.Destino!,
                ConfirmacaoAgendamento: confirmacaoRepetida);
        }

        // Nome vem do hub FHIR (não persistimos nome no smsmarica).
        var paciente = await pacientes.ObterPorIdAsync(link.PatientId, cancellationToken);
        var nome = string.IsNullOrWhiteSpace(paciente.NomeCompleto) ? "Paciente" : paciente.NomeCompleto;

        // Link de notificação de agendamento: o USO do link (1 clique no botão do WhatsApp)
        // já confirma a presença do paciente — mesmo SaveChanges do consumo do link.
        var confirmacao = await ConfirmarPresencaAsync(link.SolicitacaoId, cancellationToken);

        // Abre a sessão normal do cidadão (mesma do OTP) e salva (persiste também o link).
        var (jwt, _) = await sessoes.AbrirSessaoAsync(
            link.PatientId, nome, link.Cpf, "magic-link", dispositivo, ip, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Login por magic-link comprova que o WhatsApp CHEGOU no número e que ele é válido →
        // marca o contato como verificado (idempotente). Best-effort: nunca impede o login.
        // O número verificado é o que RECEBEU este link (o da comunicação) — não o "principal" do
        // cadastro, que pode ser outro número e nunca ter recebido nada. Link sem comunicação
        // (gerado à mão) não prova número nenhum.
        try
        {
            var fone = await db.ComunicacoesPaciente.AsNoTracking()
                .Where(c => c.LoginLinkId == token && c.Telefone != null)
                .Select(c => c.Telefone)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(fone))
                await telefones.MarcarValidadoAsync(link.Cpf, fone, "magic-link", null, cancellationToken);
        }
        catch { /* número de outra pessoa / falha FHIR — não trava o login */ }

        return new RespostaMagicLinkDto(
            jwt,
            new PacienteSessaoDto(link.PatientId, nome, link.Cpf),
            string.IsNullOrWhiteSpace(link.Destino) ? "/" : link.Destino!,
            confirmacao);
    }

    private string MontarUrl(Guid token)
    {
        var appBase = (configuration["Publico:AppBaseUrl"] ?? "https://app.smsmarica.online").TrimEnd('/');
        return $"{appBase}/entrar/{token}";
    }

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
}
