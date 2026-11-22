import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { StudySessions } from '../../src/components/StudySessions';

// UX fix 7: an empty list is explained, with a next step.
describe('UX fix 7: an empty list says why it is empty', () => {
  const json = (status, body) => Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });

  beforeEach(() => {
    global.fetch = jest.fn(() => json(200, []));
  });

  afterEach(() => {
    delete global.fetch;
  });

  it('invites the user to host the first session when there are none', async () => {
    render(<StudySessions />);

    expect(await screen.findByText('No study sessions yet. Host the first one below.')).toBeTruthy();
  });

  it('offers to clear the filters when they match nothing', async () => {
    render(<StudySessions />);
    await screen.findByText('No study sessions yet. Host the first one below.');
    fireEvent.change(screen.getByLabelText('Course contains'), { target: { value: 'Quantum' } });
    fireEvent.click(screen.getByRole('button', { name: 'Apply filters' }));
    expect(await screen.findByText(/No study sessions match these filters/)).toBeTruthy();

    // There are two "Clear" buttons now: the filter form's own link and the one in the message.
    fireEvent.click(screen.getByRole('button', { name: 'Clear filters' }));

    expect(await screen.findByText('No study sessions yet. Host the first one below.')).toBeTruthy();
    expect(global.fetch).toHaveBeenLastCalledWith('api/studysessions');
  });
});
