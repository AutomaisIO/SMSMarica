import { useState } from 'react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { notificar } from '@/shared/ui/Notificacoes';
import { useAbrirTicket } from '@/features/tickets/api/queries';
import { AnexosInput } from '@/features/tickets/components/AnexosInput';
import type { AnexoRef, TicketTipo } from '@/features/tickets/types';
import { ROTULO_TIPO } from '@/features/tickets/types';

const TIPOS: TicketTipo[] = ['Bug', 'Mudanca', 'Sugestao', 'Duvida'];

const DICA_TIPO: Record<TicketTipo, string> = {
  Bug: 'Algo está com erro ou se comportando de forma errada.',
  Mudanca: 'Ajuste em algo que já existe.',
  Sugestao: 'Ideia de melhoria ou funcionalidade nova.',
  Duvida: 'Dúvida sobre como usar o sistema.',
};

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  aoCriar?: (id: string) => void;
};

export function AbrirTicketModal({ aberto, aoFechar, aoCriar }: Props) {
  const [tipo, setTipo] = useState<TicketTipo>('Bug');
  const [titulo, setTitulo] = useState('');
  const [descricao, setDescricao] = useState('');
  const [anexos, setAnexos] = useState<AnexoRef[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [criadoId, setCriadoId] = useState<string | null>(null);
  const abrir = useAbrirTicket();

  function reiniciar() {
    setTipo('Bug');
    setTitulo('');
    setDescricao('');
    setAnexos([]);
    setErro(null);
    setCriadoId(null);
  }

  function fechar() {
    if (abrir.isPending) return;
    reiniciar();
    aoFechar();
  }

  async function enviar() {
    setErro(null);
    if (!titulo.trim() || !descricao.trim()) {
      setErro('Preencha o título e a descrição.');
      return;
    }
    try {
      const id = await abrir.mutateAsync({ titulo: titulo.trim(), descricao: descricao.trim(), tipo, anexos });
      notificar('Ticket aberto! Você será avisado quando houver retorno.', 'sucesso');
      setCriadoId(id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <Modal aberto={aberto} aoFechar={fechar} titulo="Abrir ticket" descricao="Reporte um bug, peça uma mudança, sugira uma melhoria ou tire uma dúvida." largura="lg">
      {criadoId ? (
        <div className="space-y-4">
          <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-3">
            <p className="text-sm font-medium text-green-800">Ticket aberto com sucesso!</p>
            <p className="mt-1 text-sm text-green-700">
              Você será avisado quando houver retorno. Quer abrir outro agora?
            </p>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <Button variante="ghost" onClick={fechar}>Fechar</Button>
            <Button variante="ghost" onClick={() => { const id = criadoId; reiniciar(); aoFechar(); aoCriar?.(id); }}>
              Ver ticket
            </Button>
            <Button onClick={reiniciar}>Abrir outro</Button>
          </div>
        </div>
      ) : (
      <div className="space-y-4">
        <Campo label="O que você quer fazer?" htmlFor="tk-tipo" required dica={DICA_TIPO[tipo]}>
          <Select id="tk-tipo" value={tipo} onChange={(e) => setTipo(e.target.value as TicketTipo)}>
            {TIPOS.map((t) => (
              <option key={t} value={t}>{ROTULO_TIPO[t]}</option>
            ))}
          </Select>
        </Campo>

        <Campo label="Título" htmlFor="tk-titulo" required>
          <Input
            id="tk-titulo"
            value={titulo}
            maxLength={200}
            placeholder="Resuma em uma frase"
            onChange={(e) => setTitulo(e.target.value)}
          />
        </Campo>

        <Campo label="Descrição" htmlFor="tk-descricao" required dica="Descreva com detalhes. Em um bug, diga o que fez e o que esperava.">
          <textarea
            id="tk-descricao"
            value={descricao}
            maxLength={5000}
            rows={6}
            placeholder="Descreva aqui…"
            onChange={(e) => setDescricao(e.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-red-500 focus:outline-none focus:ring-1 focus:ring-red-500"
          />
        </Campo>

        <Campo label="Anexos (opcional)" htmlFor="tk-anexos" dica="Prints ajudam muito a entender o problema.">
          <AnexosInput anexos={anexos} aoMudar={setAnexos} disabled={abrir.isPending} />
        </Campo>

        {erro && <p className="text-sm text-red-600">{erro}</p>}

        <div className="flex justify-end gap-2 pt-2">
          <Button variante="ghost" onClick={fechar} disabled={abrir.isPending}>Cancelar</Button>
          <Button onClick={enviar} disabled={abrir.isPending}>
            {abrir.isPending ? 'Enviando…' : 'Abrir ticket'}
          </Button>
        </div>
      </div>
      )}
    </Modal>
  );
}
