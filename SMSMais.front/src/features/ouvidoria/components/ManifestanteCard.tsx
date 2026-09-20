import { useState } from 'react';
import { EyeOff, KeyRound, Loader2, ShieldAlert, UserRound } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarCpf } from '@/shared/lib/cpf';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Modal } from '@/shared/ui/Modal';
import { useRevelarIdentidade } from '@/features/ouvidoria/api/queries';
import { Textarea } from '@/features/ouvidoria/components/Textarea';
import type { ManifestacaoDetalheDto, ManifestanteDto } from '@/features/ouvidoria/types';

const MIN_JUSTIFICATIVA = 15;

type Props = { manifestacao: ManifestacaoDetalheDto };

/**
 * Dados do manifestante. Quando `identidadeRestrita`, NUNCA renderiza nada além de "Identidade
 * restrita" — e só quem tem `OuvidoriaSigilo` vê o botão de revelar, que exige justificativa e
 * fica registrado (Decreto 10.153 art. 6º §3º).
 */
export function ManifestanteCard({ manifestacao: m }: Props) {
  const podeRevelar = usePermissao('OuvidoriaSigilo', 'Consulta');
  const [revelado, setRevelado] = useState<ManifestanteDto | null>(null);
  const [modalAberto, setModalAberto] = useState(false);
  const [justificativa, setJustificativa] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const revelar = useRevelarIdentidade(m.id);

  if (m.identificacao === 'Anonima') {
    return (
      <Bloco titulo="Manifestante">
        <p className="flex items-center gap-2 text-sm text-slate-600">
          <UserRound className="h-4 w-4 text-slate-400" aria-hidden="true" />
          Manifestação anônima — sem dados do manifestante e sem código de acesso.
        </p>
      </Bloco>
    );
  }

  const dados = revelado ?? (m.identidadeRestrita ? null : m.manifestante);

  async function confirmarRevelacao() {
    setErro(null);
    if (justificativa.trim().length < MIN_JUSTIFICATIVA) {
      setErro(`Escreva uma justificativa com pelo menos ${MIN_JUSTIFICATIVA} caracteres.`);
      return;
    }
    try {
      const r = await revelar.mutateAsync(justificativa.trim());
      setRevelado(r);
      setModalAberto(false);
      setJustificativa('');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <Bloco titulo="Manifestante">
      {dados ? (
        <dl className="grid gap-x-4 gap-y-1 text-sm sm:grid-cols-2">
          <Item rotulo="Nome" valor={dados.nome} />
          <Item rotulo="CPF" valor={dados.cpf ? formatarCpf(dados.cpf) : null} />
          <Item rotulo="Telefone" valor={dados.telefone} />
          <Item rotulo="E-mail" valor={dados.email} />
        </dl>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="flex items-center gap-2 text-sm text-slate-600">
            <EyeOff className="h-4 w-4 text-purple-500" aria-hidden="true" />
            Identidade restrita.
          </p>
          {podeRevelar ? (
            <Button type="button" variante="outline" tamanho="sm" onClick={() => setModalAberto(true)}>
              <KeyRound className="h-4 w-4" aria-hidden="true" /> Revelar identidade
            </Button>
          ) : null}
        </div>
      )}
      {revelado ? (
        <p className="mt-2 text-xs text-purple-700">Identidade revelada nesta sessão — o acesso ficou registrado com seu nome e data.</p>
      ) : null}

      <Modal
        aberto={modalAberto}
        aoFechar={() => {
          if (revelar.isPending) return;
          setModalAberto(false);
          setErro(null);
        }}
        titulo="Revelar identidade do manifestante"
        descricao="Acesso excepcional a dado protegido. Só faça se for indispensável ao tratamento."
        largura="sm"
      >
        <div className="space-y-4">
          <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
            <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <span>
              <strong>Este acesso fica registrado com seu nome e data</strong>, junto com a justificativa, na trilha da
              manifestação e na auditoria do sistema.
            </span>
          </div>
          <Campo
            label="Justificativa"
            htmlFor="ouv-justificativa-identidade"
            required
            erro={erro ?? undefined}
            dica={`Mínimo de ${MIN_JUSTIFICATIVA} caracteres. Diga por que precisa ver a identidade.`}
          >
            <Textarea
              id="ouv-justificativa-identidade"
              value={justificativa}
              onChange={(e) => setJustificativa(e.target.value)}
              rows={3}
              maxLength={500}
              autoFocus
            />
          </Campo>
          <div className="flex justify-end gap-2">
            <Button type="button" variante="ghost" onClick={() => setModalAberto(false)} disabled={revelar.isPending}>
              Cancelar
            </Button>
            <Button type="button" onClick={confirmarRevelacao} disabled={revelar.isPending || justificativa.trim().length < MIN_JUSTIFICATIVA}>
              {revelar.isPending ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <KeyRound className="h-4 w-4" aria-hidden="true" />}
              Revelar
            </Button>
          </div>
        </div>
      </Modal>
    </Bloco>
  );
}

export function Bloco({ titulo, children, acao }: { titulo: string; children: React.ReactNode; acao?: React.ReactNode }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-2 flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-slate-700">{titulo}</h3>
        {acao}
      </div>
      {children}
    </section>
  );
}

export function Item({ rotulo, valor }: { rotulo: string; valor: React.ReactNode | null | undefined }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{rotulo}</dt>
      <dd className="text-slate-800">{valor === null || valor === undefined || valor === '' ? '—' : valor}</dd>
    </div>
  );
}
