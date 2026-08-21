using System.Text;
using System.Text.Json;
using Automais.Zap.Core.Meta;
using FluentAssertions;

namespace Automais.Zap.Tests;

public sealed class PayloadMetaTests
{
    /// <summary>Um POST com dois WABAs diferentes — o caso que obriga o recorte.</summary>
    private const string DoisDonos = """
    {
      "object": "whatsapp_business_account",
      "entry": [
        {
          "id": "WABA_A",
          "changes": [
            {
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "metadata": { "display_phone_number": "5521999990000", "phone_number_id": "NUM_A" },
                "messages": [ { "id": "wamid.A", "from": "5521888880000", "type": "text" } ]
              }
            }
          ]
        },
        {
          "id": "WABA_B",
          "changes": [
            {
              "field": "statuses",
              "value": {
                "messaging_product": "whatsapp",
                "metadata": { "display_phone_number": "5511977770000", "phone_number_id": "NUM_B" },
                "statuses": [ { "id": "wamid.B", "status": "delivered" } ]
              }
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void Le_um_evento_por_change_com_o_numero_e_o_waba()
    {
        PayloadMeta.TentarLer(Encoding.UTF8.GetBytes(DoisDonos), out var payload, out var erro)
            .Should().BeTrue();
        erro.Should().BeNull();

        using var p = payload!;
        p.Eventos.Should().HaveCount(2);

        p.Eventos[0].PhoneNumberId.Should().Be("NUM_A");
        p.Eventos[0].WabaId.Should().Be("WABA_A");
        p.Eventos[0].Field.Should().Be("messages");

        p.Eventos[1].PhoneNumberId.Should().Be("NUM_B");
        p.Eventos[1].Field.Should().Be("statuses");
    }

    [Fact]
    public void Fatiar_entrega_a_cada_dono_so_o_que_e_dele()
    {
        PayloadMeta.TentarLer(Encoding.UTF8.GetBytes(DoisDonos), out var payload, out _).Should().BeTrue();
        using var p = payload!;

        var soA = Encoding.UTF8.GetString(p.Fatiar(new HashSet<(int, int)> { (0, 0) }));

        soA.Should().Contain("NUM_A");
        // O ponto do recorte: o dono do NUM_A não pode ver nada do NUM_B.
        soA.Should().NotContain("NUM_B");
        soA.Should().NotContain("wamid.B");
        soA.Should().NotContain("WABA_B");
    }

    [Fact]
    public void Fatiar_preserva_o_envelope_e_a_forma_do_payload()
    {
        PayloadMeta.TentarLer(Encoding.UTF8.GetBytes(DoisDonos), out var payload, out _).Should().BeTrue();
        using var p = payload!;

        var recorte = p.Fatiar(new HashSet<(int, int)> { (1, 0) });

        using var doc = JsonDocument.Parse(recorte);
        doc.RootElement.GetProperty("object").GetString().Should().Be("whatsapp_business_account");

        var entries = doc.RootElement.GetProperty("entry");
        entries.GetArrayLength().Should().Be(1);
        entries[0].GetProperty("id").GetString().Should().Be("WABA_B");
        entries[0].GetProperty("changes").GetArrayLength().Should().Be(1);
        entries[0].GetProperty("changes")[0]
            .GetProperty("value").GetProperty("metadata").GetProperty("phone_number_id")
            .GetString().Should().Be("NUM_B");
    }

    [Fact]
    public void Change_sem_numero_ainda_traz_o_waba_para_a_rota_de_reserva()
    {
        const string statusDeTemplate = """
        {
          "object": "whatsapp_business_account",
          "entry": [
            {
              "id": "WABA_A",
              "changes": [ { "field": "message_template_status_update", "value": { "event": "APPROVED" } } ]
            }
          ]
        }
        """;

        PayloadMeta.TentarLer(Encoding.UTF8.GetBytes(statusDeTemplate), out var payload, out _).Should().BeTrue();
        using var p = payload!;

        p.Eventos.Should().ContainSingle();
        p.Eventos[0].PhoneNumberId.Should().BeNull();
        p.Eventos[0].WabaId.Should().Be("WABA_A");
        p.Eventos[0].Field.Should().Be("message_template_status_update");
    }

    [Fact]
    public void Payload_sem_entry_le_sem_estourar_e_sem_eventos()
    {
        PayloadMeta.TentarLer("""{"object":"whatsapp_business_account"}"""u8, out var payload, out var erro)
            .Should().BeTrue();
        erro.Should().BeNull();

        using var p = payload!;
        p.Eventos.Should().BeEmpty();
    }

    [Fact]
    public void Json_invalido_nao_estoura_devolve_erro()
    {
        PayloadMeta.TentarLer("nao é json"u8, out var payload, out var erro).Should().BeFalse();
        payload.Should().BeNull();
        erro.Should().NotBeNullOrEmpty();
    }
}
