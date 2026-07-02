import { useState } from 'react';
import { Check, LogIn, Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { formatarInstanteData } from '@/shared/lib/datas';
import { gerarLinkAcesso } from '@/features/solicitacoes-exame/api/solicitacoesExameApi';

/**
 * Gera um "magic-link" de acesso (login em 1 clique) do paciente e copia para a área de
 * transferência — para enviar por WhatsApp. Uso único, validade curta (Config de Laudo).
 */
export function BotaoLinkAcesso({ solicitacaoId }: { solicitacaoId: string }) {
  const [carregando, setCarregando] = useState(false);
  const [copiado, setCopiado] = useState(false);

  async function gerar() {
    if (carregando) return;
    setCarregando(true);
    try {
      const r = await gerarLinkAcesso(solicitacaoId);
      try {
        await navigator.clipboard.writeText(r.url);
        setCopiado(true);
        setTimeout(() => setCopiado(false), 3000);
      } catch {
        /* clipboard pode falhar — mostra no alerta abaixo */
      }
      const exp = formatarInstanteData(r.expiraEm);
      alert(`Link de acesso (login em 1 clique) gerado e copiado:\n\n${r.url}\n\nUso único — válido até ${exp}.`);
    } catch {
      alert('Não foi possível gerar o link de acesso.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <Button variante="outline" onClick={gerar} disabled={carregando}>
      {carregando ? (
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
      ) : copiado ? (
        <Check className="mr-2 h-4 w-4 text-green-600" />
      ) : (
        <LogIn className="mr-2 h-4 w-4" />
      )}
      {copiado ? 'Link copiado' : 'Gerar link de acesso'}
    </Button>
  );
}
