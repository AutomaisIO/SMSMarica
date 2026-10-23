import { Component, type ReactNode } from 'react';

/**
 * Isola uma parte da tela: se ela quebrar ao desenhar, some sozinha e o resto do app segue. Usada nas
 * peças novas da localização (ADR-0079 §9): elas não podem derrubar o que já funciona.
 */
export class ProtecaoDeErro extends Component<{ children: ReactNode }, { falhou: boolean }> {
  state = { falhou: false };

  static getDerivedStateFromError() {
    return { falhou: true };
  }

  componentDidCatch() {
    // A parte some; nada mais a fazer.
  }

  render() {
    return this.state.falhou ? null : this.props.children;
  }
}
