# Vocalith Runtime Automation

`com.vocalith.runtime-automation` provides a game-agnostic runtime automation protocol for Unity Development Players. Games register providers for structured state, semantic entities, bounded actions, events, diagnostics, visual evidence, and explicitly gated QA mutations.

The package never discovers or mutates private game state by reflection. Game-specific behavior belongs in an adapter assembly such as `Lilith.Automation`.
