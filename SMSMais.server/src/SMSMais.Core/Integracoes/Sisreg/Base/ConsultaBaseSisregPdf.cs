using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Exames;
using SMSMais.Core.Integracoes.Sisreg.Base.Dtos;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMais.Core.Integracoes.Sisreg.Base;

/// <summary>
/// Relatório nominal da consulta SISREG na nossa base: A4 paisagem, identidade visual da
/// instituição, o filtro por extenso no topo (quem recebe o papel precisa saber o recorte) e uma
/// linha por atendimento — paciente, data do agendamento, procedimento e situação.
/// </summary>
internal sealed class ConsultaBaseSisregPdf(
    ConsultaBaseSisregFiltro filtro,
    IReadOnlyList<AgendamentoBaseSisregDto> itens,
    IReadOnlyList<string> nomesUnidades,
    DateTime geradoEmUtc,
    IdentidadeVisualPdf idv)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private const string Tinta = "#1f2328";
    private const string Suave = "#57606a";
    private const string Linha = "#d0d7de";
    private const string Zebra = "#f6f8fa";

    public byte[] Gerar() => QuestDocument.Create(Compor).GeneratePdf();

    public static string Rotulo(SituacaoAgendamentoSisreg s) => s switch
    {
        SituacaoAgendamentoSisreg.NaFila => "Na fila",
        SituacaoAgendamentoSisreg.Agendada => "Agendada",
        SituacaoAgendamentoSisreg.Pendente => "Pendente de atualização",
        SituacaoAgendamentoSisreg.Compareceu => "Compareceu",
        SituacaoAgendamentoSisreg.Faltou => "Faltou",
        SituacaoAgendamentoSisreg.Cancelada => "Cancelada",
        _ => s.ToString(),
    };

    private void Compor(IDocumentContainer doc)
    {
        doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.MarginHorizontal(1.3f, Unit.Centimetre);
            page.MarginVertical(1.1f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(8.5f).FontFamily("Helvetica").FontColor(Tinta));
            page.Header().Element(Cabecalho);
            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(10);
                col.Item().Element(Abertura);
                col.Item().Element(Tabela);
            });
            page.Footer().Row(r =>
            {
                r.RelativeItem()
                    .Text($"Gerado em {FusoBrasilia.ParaExibicao(geradoEmUtc):dd/MM/yyyy HH:mm} · fonte: base do SMSMais "
                          + "alimentada pelo SISREG III · contém dados pessoais — uso interno (LGPD)")
                    .FontSize(7).FontColor(Suave);
                r.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Suave));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });
    }

    private void Cabecalho(IContainer c) => c.Column(col =>
    {
        col.Item().Row(row =>
        {
            if (idv.Logo is not null) row.ConstantItem(120).Height(34).AlignLeft().Image(idv.Logo).FitArea();
            row.RelativeItem().AlignRight().AlignMiddle().Column(t =>
            {
                t.Item().AlignRight().Text(idv.Nome).FontSize(10).Bold().FontColor(idv.CorPrimaria);
                t.Item().AlignRight().Text($"Consulta SISREG · {Periodo()}").FontSize(8).FontColor(Suave);
            });
        });
        col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor(idv.CorPrimaria);
    });

    private void Abertura(IContainer c) => c.Column(col =>
    {
        col.Spacing(2);
        col.Item().Text("Atendimentos do SISREG").FontSize(16).Bold().FontColor(idv.CorEscura);

        var pessoas = itens.Select(i => i.PacienteId).Distinct().Count();
        col.Item().Text(t =>
        {
            t.Span($"{pessoas.ToString("N0", PtBr)} {(pessoas == 1 ? "pessoa" : "pessoas")}").Bold();
            t.Span($" · {itens.Count.ToString("N0", PtBr)} {(itens.Count == 1 ? "atendimento" : "atendimentos")}");
        });

        col.Item().PaddingTop(4).Text(t =>
        {
            t.DefaultTextStyle(x => x.FontColor(Suave));
            Campo(t, "Período", Periodo());
            Campo(t, "Unidades", nomesUnidades.Count == 0 ? "todas" : string.Join("; ", nomesUnidades));
            Campo(t, "Situação", filtro.Situacoes is { Count: > 0 } s
                ? string.Join(", ", s.Distinct().Select(Rotulo))
                : "todas");
            Campo(t, "Procedimentos", filtro.Procedimentos is { Count: > 0 } p
                ? string.Join("; ", p.Order(StringComparer.Create(PtBr, true)))
                : "todos");
            Campo(t, "Tipo", (filtro.IncluirExames, filtro.IncluirConsultas) switch
            {
                (true, true) => "exames e consultas",
                (true, false) => "exames",
                _ => "consultas",
            }, ultimo: true);
        });
    });

    private static void Campo(TextDescriptor t, string rotulo, string valor, bool ultimo = false)
    {
        t.Span($"{rotulo}: ").SemiBold().FontColor(Tinta);
        t.Span(valor);
        if (!ultimo) t.Span("   ·   ");
    }

    private string Periodo()
    {
        var eixo = filtro.Eixo == EixoDataConsultaSisreg.Solicitacao ? "solicitados" : "agendados";
        return $"{eixo} de {filtro.Inicio:dd/MM/yyyy} a {filtro.Fim:dd/MM/yyyy}";
    }

    private void Tabela(IContainer c) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.ConstantColumn(28);
            cd.RelativeColumn(3.2f);
            cd.ConstantColumn(80);
            cd.RelativeColumn(4.5f);
            cd.ConstantColumn(110);
        });

        t.Header(h =>
        {
            foreach (var titulo in new[] { "#", "Paciente", "Agendamento", "Procedimento", "Situação" })
                h.Cell().Background(idv.CorPrimaria).PaddingVertical(4).PaddingHorizontal(4)
                    .Text(titulo).FontColor(Colors.White).Bold().FontSize(8);
        });

        for (var i = 0; i < itens.Count; i++)
        {
            var it = itens[i];
            var fundo = i % 2 == 0 ? "#ffffff" : Zebra;
            IContainer Celula(IContainer x) => x.Background(fundo).BorderBottom(0.5f).BorderColor(Linha)
                .PaddingVertical(3).PaddingHorizontal(4);

            t.Cell().Element(Celula).AlignRight().Text((i + 1).ToString(PtBr)).FontColor(Suave);
            t.Cell().Element(Celula).Text(it.PacienteNome ?? "(paciente não encontrado no cadastro)").SemiBold();
            t.Cell().Element(Celula).Text(it.DataAgendada is { } d
                ? FusoBrasilia.ParaExibicao(d).ToString("dd/MM/yyyy HH:mm", PtBr)
                : "—");
            t.Cell().Element(Celula).Text(it.Procedimento);
            t.Cell().Element(Celula).Text(Rotulo(it.Situacao));
        }
    });
}
