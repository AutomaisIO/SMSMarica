import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { UNSAFE_NavigationContext } from 'react-router-dom';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';

type Opcoes = {
  /** Há alterações não salvas? Quando `false`, nenhum guard fica ativo. */
  sujo: boolean;
  /**
   * Persiste as alterações (somente salvar — sem navegar). Deve lançar em caso de
   * erro para o "Salvar e sair" não prosseguir. Se omitido, o modal só oferece
   * "Sair sem salvar" / "Continuar editando".
   */
  aoSalvar?: () => Promise<void>;
  mensagem?: string;
};

/**
 * Avisa o operador quando ele tenta sair de uma tela com alterações não salvas.
 *
 * O painel usa `<BrowserRouter>` (não é data router), então `useBlocker` do
 * react-router não está disponível. Cobrimos os caminhos reais de "trocar de tela":
 *  - navegação SPA por `<Link>`/menu/`navigate(destino)` → intercepta `push`/`replace`;
 *  - fechar/atualizar a aba → `beforeunload` (aviso nativo do browser);
 *  - botões "Voltar" internos (`navigate(-1)`, que é POP) → use `protegerAcao(fn)`.
 *
 * Navegações programáticas legítimas (depois de salvar) devem chamar `permitir()`
 * imediatamente antes do `navigate(...)` para passarem sem o modal.
 */
export function useAvisoSaidaNaoSalva({ sujo, aoSalvar, mensagem }: Opcoes) {
  const { navigator } = useContext(UNSAFE_NavigationContext);
  const nav = navigator as unknown as {
    push: (...args: unknown[]) => void;
    replace: (...args: unknown[]) => void;
  };

  const [pendente, setPendente] = useState<null | (() => void)>(null);
  const [salvando, setSalvando] = useState(false);

  const permitirRef = useRef(false);
  const sujoRef = useRef(sujo);
  sujoRef.current = sujo;

  /** Libera a PRÓXIMA navegação SPA (push/replace) do modal — usar antes de `navigate` pós-save. */
  const permitir = useCallback(() => {
    permitirRef.current = true;
  }, []);

  // Fechar/atualizar a aba: aviso nativo do navegador.
  useEffect(() => {
    if (!sujo) return;
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = '';
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [sujo]);

  // Navegação SPA (Link/menu/navigate(destino)): intercepta push/replace.
  useEffect(() => {
    if (!sujo) return;
    const nativo = nav as unknown as Record<string, (...args: unknown[]) => void>;
    const pushOrig = nativo.push.bind(nav);
    const replaceOrig = nativo.replace.bind(nav);

    const intercepta = (orig: (...args: unknown[]) => void) => (...args: unknown[]) => {
      if (permitirRef.current) {
        permitirRef.current = false;
        orig(...args);
        return;
      }
      // Segura a navegação e abre o modal; ao prosseguir, restaura e navega de verdade.
      setPendente(() => () => {
        nativo.push = pushOrig;
        nativo.replace = replaceOrig;
        orig(...args);
      });
    };

    nativo.push = intercepta(pushOrig);
    nativo.replace = intercepta(replaceOrig);
    return () => {
      nativo.push = pushOrig;
      nativo.replace = replaceOrig;
    };
  }, [sujo, nav]);

  const continuar = useCallback(() => setPendente(null), []);

  const descartar = useCallback(() => {
    setPendente((p) => {
      p?.();
      return null;
    });
  }, []);

  const salvarESair = useCallback(async () => {
    if (!aoSalvar) {
      descartar();
      return;
    }
    setSalvando(true);
    try {
      await aoSalvar();
      setPendente((p) => {
        p?.();
        return null;
      });
    } catch {
      // O erro fica visível na própria tela; fecha o modal para o operador ver.
      setPendente(null);
    } finally {
      setSalvando(false);
    }
  }, [aoSalvar, descartar]);

  /**
   * Envolve uma navegação POP (ex.: botão "Voltar" = `navigate(-1)`), que não passa
   * por push/replace. Se estiver sujo, abre o modal; senão executa direto.
   */
  const protegerAcao = useCallback(
    (fn: () => void) => () => {
      if (sujoRef.current) {
        setPendente(() => fn);
      } else {
        fn();
      }
    },
    [],
  );

  const elemento = (
    <Modal
      aberto={pendente !== null}
      aoFechar={continuar}
      titulo="Alterações não salvas"
      largura="sm"
    >
      <div className="space-y-4">
        <p className="text-sm text-gray-700">
          {mensagem ?? 'Há alterações que ainda não foram salvas. O que você deseja fazer?'}
        </p>
        <div className="flex flex-wrap items-center justify-end gap-3">
          <Button type="button" variante="ghost" onClick={continuar} disabled={salvando}>
            Continuar editando
          </Button>
          <Button type="button" variante="danger" onClick={descartar} disabled={salvando}>
            Sair sem salvar
          </Button>
          {aoSalvar ? (
            <Button type="button" onClick={salvarESair} disabled={salvando}>
              {salvando ? 'Salvando…' : 'Salvar e sair'}
            </Button>
          ) : null}
        </div>
      </div>
    </Modal>
  );

  return { elemento, permitir, protegerAcao };
}
