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

        var template = configuration["Pesquisa:Template"] ?? "pesquisa_de_satisfacao_2";
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
