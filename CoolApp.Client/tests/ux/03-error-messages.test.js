import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { StudySessions } from '../../src/components/StudySessions';

// UX fix 3: one message per cause, not "Please try again" for everything.
describe('UX fix 3: a message for each cause of a failed create', () => {
  it.each([
    [500, 'Something went wrong on our side'],
    [503, 'The server is busy right now'],
    [409, 'Someone changed the same data at the same time'],
    [404, 'Reload the page and try again']
  ])('HTTP %i gets its own message', (status, message) => {
    expect(StudySessions.describeCreateFailure(status)).toContain(message);
  });

  describe('on the page', () => {
    const json = (status, body) => Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });

    beforeEach(() => {
      global.fetch = jest.fn(() => json(200, []));
    });

    afterEach(() => {
      delete global.fetch;
    });

    it('shows the server-side message when the API fails with 500', async () => {
      render(<StudySessions />);
      await screen.findByRole('button', { name: 'Create session' });
      fireEvent.change(screen.getByLabelText('Course'), { target: { value: 'Software Development I' } });
      fireEvent.change(screen.getByLabelText('Topic'), { target: { value: 'Testing' } });
      fireEvent.change(screen.getByLabelText('Location'), { target: { value: 'MIF, room 401' } });
      fireEvent.change(screen.getByLabelText('Starts at'), { target: { value: '2030-01-01T18:00' } });
      fireEvent.change(screen.getByLabelText('Host name'), { target: { value: 'Ieva' } });
      global.fetch.mockImplementationOnce(() => json(500, { title: 'An error occurred' }));

      fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

      expect(await screen.findByText(/Something went wrong on our side/)).toBeTruthy();
    });
  });
});
