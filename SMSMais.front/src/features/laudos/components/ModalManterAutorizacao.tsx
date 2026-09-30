import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import type { EscolhaSessaoNuvem } from '@/features/laudos/types';

/**
 * "Manter a autorização?" (ADR-0061 §2.1) — aparece no modo Nuvem antes de ir ao app, quando o
 * médico está em "perguntar" e ainda não tem autorização neste acesso. Manter = aprova uma vez e
 * assina os próximos laudos sem o celular até sair do sistema. "Não perguntar de novo" grava a
 * resposta, qualquer que seja, como preferência dele.
 *
 * Os botões abrem a aba do VIDaaS no próprio clique (o navegador bloqueia se for depois).
 */
export function ModalManterAutorizacao({
  aberto,
  enviando,
  aoResponder,
  aoCancelar,
}: {
  aberto: boolean;
  enviando: boolean;
  aoResponder: (escolha: EscolhaSessaoNuvem) => void;
  aoCancelar: () => void;
}) {
  const [naoPerguntar, setNaoPerguntar] = useState(false);
  useEffect(() => {
    if (aberto) setNaoPerguntar(false);
  }, [aberto]);

  return (
    <Modal
      aberto={aberto}
      aoFechar={enviando ? () => {} : aoCancelar}
      titulo="Manter a autorização do VIDaaS?"
      descricao="Aprove no app agora e assine os próximos laudos sem o celular, até sair do sistema."
      largura="sm"
    >
      <div className="space-y-4">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={naoPerguntar}
            onChange={(e) => setNaoPerguntar(e.target.checked)}
            disabled={enviando}
            className="h-4 w-4 rounded border-gray-300"
          />
          Não perguntar de novo
        </label>
        <div className="flex justify-end gap-2 border-t border-gray-100 pt-4">
          <Button
            variante="outline"
            disabled={enviando}
            onClick={() => aoResponder({ manter: false, naoPerguntarDeNovo: naoPerguntar })}
          >
            Só este laudo
          </Button>
          <Button
            disabled={enviando}
            onClick={() => aoResponder({ manter: true, naoPerguntarDeNovo: naoPerguntar })}
          >
            {enviando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Manter até sair
          </Button>
        </div>
      </div>
    </Modal>
  );
}
