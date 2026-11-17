import { useContext, useId, useState } from 'react'
import * as Popover from '@radix-ui/react-popover'
import { ModalBoundaryContext } from '../Modal/ModalBoundaryContext'
import { useComboboxSearch } from '../../../hooks/useComboboxSearch'
import type { ComboboxOption } from '../Combobox/Combobox'
import { Tooltip } from '../Tooltip/Tooltip'
import styles from './MultiSelect.module.css'

interface MultiSelectProps {
  selected: string[]
  onChange: (selected: string[]) => void
  onSearch: (query: string) => Promise<ComboboxOption[]>
  onAddItem?: (opt: ComboboxOption) => Promise<void>
  placeholder?: string
  disabled?: boolean
  title?: string
}

export function MultiSelect({ selected, onChange, onSearch, onAddItem, placeholder, disabled, title }: MultiSelectProps) {
  const [query, setQuery] = useState('')
  const [isOpen, setIsOpen] = useState(false)
  const [addingValue, setAddingValue] = useState<string | null>(null)
  const listboxId = useId()
  const boundaryEl = useContext(ModalBoundaryContext)

  const { options, isSearching, highlightedIndex, handleKeyDown } = useComboboxSearch<ComboboxOption>({
    isOpen,
    query,
    onSearch,
    filterResults: (results) => results.filter((r) => !selected.includes(r.value))
  })

  const visibleOptions = options.filter((opt) => !selected.includes(opt.value))

  const selectOption = (opt: ComboboxOption) => {
    onChange([...selected, opt.value])
    setQuery('')
  }

  const addItemImmediately = async (opt: ComboboxOption) => {
    if (!onAddItem) return
    setAddingValue(opt.value)
    try {
      await onAddItem(opt)
    } finally {
      setAddingValue(null)
    }
  }

  const activateOption = (opt: ComboboxOption) => (onAddItem ? addItemImmediately(opt) : selectOption(opt))

  const isPopoverOpen = isOpen && (visibleOptions.length > 0 || isSearching)

  return (
    <div className={styles.wrapper}>
      {!onAddItem && (
        <div className={styles.chips}>
          {selected.map((value) => (
            <span key={value} className={styles.chip}>
              {value}
              <Tooltip label={`Remove ${value}`}>
                <button
                  type="button"
                  className={styles.chipRemove}
                  onClick={() => onChange(selected.filter((v) => v !== value))}
                  aria-label={`Remove ${value}`}
                  disabled={disabled}
                  title={title}
                >
                  &times;
                </button>
              </Tooltip>
            </span>
          ))}
        </div>
      )}
      <Popover.Root open={isPopoverOpen}>
        <Popover.Anchor asChild>
          <input
            type="text"
            role="combobox"
            aria-expanded={isPopoverOpen}
            aria-controls={listboxId}
            aria-activedescendant={
              highlightedIndex >= 0 ? `${listboxId}-option-${highlightedIndex}` : undefined
            }
            className={styles.input}
            value={query}
            placeholder={placeholder}
            disabled={disabled}
            title={title}
            onChange={(e) => {
              setQuery(e.target.value)
              setIsOpen(true)
            }}
            onFocus={() => setIsOpen(true)}
            onBlur={() => {
              setTimeout(() => setIsOpen(false), 150)
            }}
            onKeyDown={(e) => handleKeyDown(e, activateOption, () => setIsOpen(false), visibleOptions)}
          />
        </Popover.Anchor>
        <Popover.Portal>
          <Popover.Content
            className={styles.optionList}
            data-in-modal={boundaryEl ? 'true' : undefined}
            asChild
            side="bottom"
            align="start"
            sideOffset={4}
            avoidCollisions
            collisionBoundary={boundaryEl ?? undefined}
            onOpenAutoFocus={(e) => e.preventDefault()}
            onCloseAutoFocus={(e) => e.preventDefault()}
            onInteractOutside={(e) => e.preventDefault()}
          >
            <ul id={listboxId} role="listbox">
              {isSearching && visibleOptions.length === 0 && (
                <li className={styles.searchingRow} role="presentation">
                  Searching…
                </li>
              )}
              {visibleOptions.map((opt, index) => (
                <li
                  key={opt.value}
                  role="presentation"
                  className={onAddItem ? styles.optionRow : undefined}
                >
                  <button
                    id={`${listboxId}-option-${index}`}
                    type="button"
                    role="option"
                    aria-selected={index === highlightedIndex}
                    className={
                      index === highlightedIndex
                        ? `${styles.option} ${styles.optionHighlighted}`
                        : styles.option
                    }
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={() => activateOption(opt)}
                    disabled={onAddItem ? addingValue !== null : undefined}
                  >
                    {opt.label}
                  </button>
                  {onAddItem && (
                    <button
                      type="button"
                      className={styles.optionAddButton}
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => addItemImmediately(opt)}
                      disabled={addingValue !== null}
                    >
                      {addingValue === opt.value ? 'Adding…' : 'Add'}
                    </button>
                  )}
                </li>
              ))}
            </ul>
          </Popover.Content>
        </Popover.Portal>
      </Popover.Root>
    </div>
  )
}
