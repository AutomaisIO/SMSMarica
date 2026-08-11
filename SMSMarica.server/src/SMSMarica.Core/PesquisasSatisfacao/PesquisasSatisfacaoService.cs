using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Atendimentos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Conversas;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.PesquisasSatisfacao.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.PesquisasSatisfacao;

/// <summary>
/// Pesquisa de satisfação por atendimento. Ver <see cref="PesquisaSatisfacao"/> para o desenho
/// do token e <see cref="IPesquisasSatisfacaoService.JanelaDias"/> para o prazo.
/// </summary>
public sealed class PesquisasSatisfacaoService(
    SmsMaricaDbContext db,
    IAtendimentosService atendimentos,
    IUsuarioAtualAccessor usuarioAtual,
    IPacientesService pacientes,
    IWhatsAppCliente whatsApp,
    IConfiguration configuration) : IPesquisasSatisfacaoService
{
    public async Task<EnvioPesquisaDto> PrepararEnvioAsync(
        Guid pacienteId, Guid encounterId, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.FirstOrDefaultAsync(x => x.EncounterId == encounterId, ct);
        var jaEnviada = p?.EnviadaEm is not null;

        if (p is null)
        {
            p = await CriarAsync(pacienteId, encounterId, ct);
        }
        else if (p.PatientId != pacienteId)
        {
            // O atendimento é de outra pessoa: enviar aqui mandaria o convite ao paciente errado.
            throw new ConflitoException(
                "pesquisa.paciente_divergente",
                "Este atendimento pertence a outro paciente.");
        }

        if (DateTime.UtcNow > p.ExpiraEm)
            throw new ConflitoException(
                "pesquisa.fora_da_janela",
                $"O prazo de {IPesquisasSatisfacaoService.JanelaDias} dias para avaliar este atendimento já passou.");

        p.EnviadaEm = DateTime.UtcNow;
        p.EnviadaPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        return new EnvioPesquisaDto(p.Id, MontarUrl(p.Id), jaEnviada);
    }

    public async Task<EnvioPesquisaDto> EnviarAsync(
        Guid pacienteId, Guid encounterId, CancellationToken ct = default)
    {
        var envio = await PrepararEnvioAsync(pacienteId, encounterId, ct);
        var p = await db.PesquisasSatisfacao.AsNoTracking().FirstAsync(x => x.Id == envio.PesquisaId, ct);
        var paciente = await pacientes.ObterPorIdAsync(pacienteId, ct);

        // Mesma régua do resto das comunicações: contato verificado primeiro, qualquer celular
        // do cadastro depois. Aqui NÃO se exige verificação — a pesquisa não carrega resultado
        // nem laudo, e exigir contato verificado deixaria de fora justamente quem a recepção
        // ainda não alcançou.
        var telefone = paciente.TelefoneVerificado
            ?? new[] { paciente.TelefoneCelular, paciente.TelefonePrincipal, paciente.TelefoneResidencial }
                .FirstOrDefault(TelefoneWhatsApp.EhCelularBr);

        if (!TelefoneWhatsApp.EhCelularBr(telefone))
            throw new ConflitoException(
                "pesquisa.sem_telefone", "Paciente sem número de celular válido para receber a pesquisa.");

        // O corpo do template diz "recebido em nossa unidade {{2}}". Sem o nome real, a frase
        // sairia "em nossa unidade nossa unidade" — e, pior, afirmaria ao paciente algo que não
        // conferimos. Recusa é o comportamento certo: a unidade vem do serviceProvider do
        // Encounter (ADR-0039) e está preenchida em 100% dos atendimentos recentes; se faltar,
        // é sinal de dado incompleto, não de mensagem a improvisar.
        if (string.IsNullOrWhiteSpace(p.UnidadeNome))
            throw new ConflitoException(
                "pesquisa.sem_unidade",
                "O atendimento não tem unidade registrada — não dá para citar o local na mensagem.");

        var template = configuration["Pesquisa:Template"] ?? "pesquisa_de_satisfacao_uri";
        var idioma = configuration["Pesquisa:TemplateIdioma"] ?? "pt_BR";

        var resultado = await whatsApp.EnviarTemplateComBotoesAsync(
            TelefoneWhatsApp.NormalizarNonoDigito(telefone!),
            template,
            idioma,
            [
                Tratamento(paciente.NomeCompleto, paciente.Sexo),
                p.UnidadeNome,
                FusoBrasilia.ParaExibicao(p.AtendimentoEm).ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")),
            ],
            // Só o botão de URL entra no payload: as duas respostas rápidas do template são
            // estáticas. O índice 0 é o do botão de link, que é o primeiro do template aprovado.
            [new BotaoTemplateWhatsApp(TipoBotaoTemplate.Url, envio.PesquisaId.ToString())],
            pacienteId,
            ct);

        if (!resultado.Ok)
            throw new ConflitoException("pesquisa.falha_envio", resultado.Erro ?? "Falha ao enviar a pesquisa.");

        // O wamid é a ponte para `whatsapp_mensagem`, onde o webhook grava entregue/lida. Sem
        // ele o painel não teria "vistas" — e duplicar esse estado aqui só criaria divergência.
        var linha = await db.PesquisasSatisfacao.FirstAsync(x => x.Id == envio.PesquisaId, ct);
        linha.WaMessageId = resultado.WaMessageId;
        linha.PacienteSexo = paciente.Sexo;
        linha.PacienteNascimento = paciente.DataNascimento;
        await db.SaveChangesAsync(ct);

        return envio;
    }

    /// <summary>Sr./Sra. + primeiro nome; sem sexo no cadastro, só o primeiro nome.</summary>
    private static string Tratamento(string? nomeCompleto, Sexo? sexo)
    {
        var primeiro = (nomeCompleto ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "paciente";
        return sexo switch
        {
            Sexo.Masculino => $"Sr. {primeiro}",
            Sexo.Feminino => $"Sra. {primeiro}",
            _ => primeiro,
        };
    }

    public async Task<string> RegistrarCliqueAsync(Guid token, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.FirstOrDefaultAsync(x => x.Id == token, ct)
            ?? throw new NaoEncontradoException("Pesquisa de satisfação", token);

        var destino = await DestinoDaUnidadeAsync(p.UnidadeCnes, ct);

        // Conta mesmo fora da janela: o clique aconteceu, e esconder isso do painel só produz
        // taxa de engajamento errada. Quem decide se ainda aceita resposta é a AvanteSocial.
        p.ClicadaEm ??= DateTime.UtcNow;
        p.Cliques++;
        await db.SaveChangesAsync(ct);

        return destino;
    }

    /// <summary>
    /// Link da AvanteSocial configurado para a unidade do atendimento, resolvido por CNES —
    /// a mesma chave que o ADR-0039 usa para casar unidade entre PEPs, e a que a planilha da
    /// AvanteSocial traz.
    /// </summary>
    private async Task<string> DestinoDaUnidadeAsync(string? cnes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cnes))
            throw new ConflitoException(
                "pesquisa.sem_cnes", "O atendimento não tem unidade identificada por CNES.");

        var destino = await (
            from u in db.Unidades.AsNoTracking()
            join c in db.UnidadePesquisaConfigs.AsNoTracking() on u.Id equals c.UnidadeId
            where u.Cnes == cnes
            select c.LinkResponder).FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(destino))
            throw new ConflitoException(
                "pesquisa.sem_link",
                "A unidade deste atendimento não tem link de pesquisa configurado.");

        return destino;
    }

    public async Task<PesquisaConfigDto> ObterConfigAsync(Guid unidadeId, CancellationToken ct = default)
    {
        var u = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == unidadeId, ct)
            ?? throw new NaoEncontradoException("Unidade", unidadeId);
        var c = await db.UnidadePesquisaConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.UnidadeId == unidadeId, ct);

        return new PesquisaConfigDto(
            u.Id, u.Nome, u.Cnes,
            c?.EnvioWhatsAppAtivo ?? false,
            c?.LinkResponder, c?.LinkPainel,
            c?.HorasAposAtendimento ?? 24,
            c?.AtualizadoEm);
    }

    public async Task<PesquisaConfigDto> SalvarConfigAsync(
        Guid unidadeId, SalvarPesquisaConfigRequest request, CancellationToken ct = default)
    {
        _ = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == unidadeId, ct)
            ?? throw new NaoEncontradoException("Unidade", unidadeId);

        // Ligar o disparo sem destino manda o paciente para lugar nenhum: o redirect recusaria e
        // ele receberia uma mensagem que não abre. Melhor barrar aqui, onde dá para explicar.
        if (request.EnvioWhatsAppAtivo && string.IsNullOrWhiteSpace(request.LinkResponder))
            throw new ValidacaoException(
                "linkResponder", "Informe o link da pesquisa antes de ligar o envio por WhatsApp.");

        if (request.HorasAposAtendimento is < 1 or > 168)
            throw new ValidacaoException(
                "horasAposAtendimento", "O atraso do envio deve ficar entre 1 hora e 7 dias.");

        var c = await db.UnidadePesquisaConfigs.FirstOrDefaultAsync(x => x.UnidadeId == unidadeId, ct);
        if (c is null)
        {
            c = new UnidadePesquisaConfig { UnidadeId = unidadeId };
            db.UnidadePesquisaConfigs.Add(c);
        }

        c.EnvioWhatsAppAtivo = request.EnvioWhatsAppAtivo;
        c.LinkResponder = string.IsNullOrWhiteSpace(request.LinkResponder) ? null : request.LinkResponder.Trim();
        c.LinkPainel = string.IsNullOrWhiteSpace(request.LinkPainel) ? null : request.LinkPainel.Trim();
        c.HorasAposAtendimento = request.HorasAposAtendimento;
        c.AtualizadoEm = DateTime.UtcNow;
        c.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        return await ObterConfigAsync(unidadeId, ct);
    }

    public async Task<PesquisaPainelDto> ObterPainelAsync(Guid unidadeId, int dias, CancellationToken ct = default)
    {
        var u = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == unidadeId, ct)
            ?? throw new NaoEncontradoException("Unidade", unidadeId);
        var desde = DateTime.UtcNow.AddDays(-Math.Clamp(dias, 1, 365));

        var convites = await db.PesquisasSatisfacao.AsNoTracking()
            .Where(p => p.UnidadeCnes == u.Cnes && p.EnviadaEm != null && p.EnviadaEm >= desde)
            .Select(p => new
            {
                p.WaMessageId, p.ClicadaEm, p.EnviadaEm, p.PacienteSexo, p.PacienteNascimento, p.AtendimentoEm,
            })
            .ToListAsync(ct);

        // Entregue/lida vêm de `whatsapp_mensagem`, alimentada pelo webhook. Não duplicamos esse
        // estado aqui: duas fontes para o mesmo fato divergem, e a do webhook é a verdadeira.
        var wamids = convites.Select(c => c.WaMessageId).Where(w => w != null).ToList();
        var status = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.WaMessageId != null && wamids.Contains(m.WaMessageId))
            .Select(m => m.Status)
            .ToListAsync(ct);

        var clicados = convites.Where(c => c.ClicadaEm is not null).ToList();
        var horas = clicados
            .Where(c => c.EnviadaEm is not null)
            .Select(c => (c.ClicadaEm!.Value - c.EnviadaEm!.Value).TotalHours)
            .OrderBy(h => h).ToList();

        var perfil = clicados
            .GroupBy(c => new
            {
                Sexo = c.PacienteSexo?.ToString() ?? "Não informado",
                Faixa = FaixaEtaria(c.PacienteNascimento, c.AtendimentoEm),
            })
            .Select(g => new PesquisaPerfilDto(g.Key.Sexo, g.Key.Faixa, g.Count()))
            .OrderByDescending(x => x.Cliques)
            .ToList();

        return new PesquisaPainelDto(
            Enviadas: convites.Count,
            Entregues: status.Count(s => s is StatusMensagemWhatsApp.Entregue or StatusMensagemWhatsApp.Lida),
            Vistas: status.Count(s => s == StatusMensagemWhatsApp.Lida),
            Clicadas: clicados.Count,
            HorasMedianasAteClique: horas.Count == 0 ? null : Math.Round(horas[horas.Count / 2], 1),
            Perfil: perfil);
    }

    /// <summary>Faixas amplas de propósito: com poucas respostas, faixa estreita identifica gente.</summary>
    private static string FaixaEtaria(DateOnly? nascimento, DateTime referencia)
    {
        if (nascimento is not { } n) return "Não informada";
        var idade = referencia.Year - n.Year;
        if (DateOnly.FromDateTime(referencia) < n.AddYears(idade)) idade--;
        return idade switch
        {
            < 0 => "Não informada",
            < 18 => "0–17",
            < 30 => "18–29",
            < 45 => "30–44",
            < 60 => "45–59",
            _ => "60+",
        };
    }

    /// <summary>
    /// Cria a pesquisa a partir do atendimento no hub. Passa pelo serviço de atendimentos do
    /// PACIENTE de propósito: é o que garante que ninguém abra pesquisa de encontro alheio.
    /// </summary>
    private async Task<PesquisaSatisfacao> CriarAsync(Guid pacienteId, Guid encounterId, CancellationToken ct)
    {
        var lista = await atendimentos.ObterPorPacienteAsync(pacienteId, ct);
        var a = lista.FirstOrDefault(x => x.Id == encounterId)
            ?? throw new NaoEncontradoException("Atendimento", encounterId);

        // O fim do atendimento é o marco. Sem ele o atendimento não terminou — e não há o que avaliar.
        var fim = (a.Fim ?? a.Inicio)?.UtcDateTime
            ?? throw new ConflitoException(
                "pesquisa.atendimento_sem_data", "Este atendimento ainda não tem data de saída.");

        var p = new PesquisaSatisfacao
        {
            Id = Guid.CreateVersion7(),
            PatientId = pacienteId,
            EncounterId = encounterId,
            // ADR-0039: a unidade é dimensão própria, não deriva do meta.source. Guardar o nome
            // aqui congela o que valia no atendimento — unidade renomeada depois não reescreve o
            // passado de quem já respondeu, e a mensagem cita o lugar onde a pessoa esteve.
            UnidadeNome = a.UnidadeNome,
            UnidadeCnes = a.UnidadeCnes,
            AtendimentoEm = fim,
            ExpiraEm = fim.AddDays(IPesquisasSatisfacaoService.JanelaDias),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.PesquisasSatisfacao.Add(p);
        return p;
    }

    private string MontarUrl(Guid token)
    {
        var appBase = (configuration["Publico:AppBaseUrl"] ?? "https://app.smsmarica.online").TrimEnd('/');
        return $"{appBase}/pesquisa/{token}";
    }
}
